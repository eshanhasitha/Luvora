using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;

namespace OrderService.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/orders")]
public sealed class AdminOrdersController(OrderDbContext db) : ControllerBase
{
    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> Get(Guid orderId)
    {
        var order = await db.Orders.AsNoTracking().Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == orderId);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpGet("{orderId:guid}/items")]
    public async Task<IActionResult> Items(Guid orderId)
    {
        if (!await db.Orders.AnyAsync(x => x.Id == orderId)) return NotFound();
        return Ok(await db.OrderItems.AsNoTracking().Where(x => x.OrderId == orderId).OrderBy(x => x.CreatedAt).ToListAsync());
    }

    [HttpPost("{orderId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid orderId)
    {
        var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == orderId);
        if (order is null) return NotFound();
        if (order.Status is not ("CREATED" or "PAYMENT_PENDING")) return Conflict(new { message = "Order cannot be cancelled in its current status.", currentStatus = order.Status });
        order.Status = "CANCELLED"; order.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync();
        return Ok(new { order.Id, order.OrderNumber, order.Status });
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] Guid? customerId,
        [FromQuery] string? orderNumber, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Orders.AsNoTracking().Include(x => x.Items).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (customerId.HasValue) query = query.Where(x => x.UserId == customerId);
        if (!string.IsNullOrWhiteSpace(orderNumber)) query = query.Where(x => x.OrderNumber.Contains(orderNumber));
        if (from.HasValue) query = query.Where(x => x.CreatedAt >= from);
        if (to.HasValue) query = query.Where(x => x.CreatedAt < to.Value.AddDays(1));
        var total = await query.CountAsync();
        return Ok(new { page, pageSize, totalItems = total, orders = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync() });
    }

    [HttpPatch("{orderId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid orderId, [FromBody] StatusRequest request)
    {
        var order = await db.Orders.FirstOrDefaultAsync(x => x.Id == orderId);
        if (order is null) return NotFound();
        var transitions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["CREATED"] = ["PAYMENT_PENDING", "CONFIRMED", "CANCELLED"],
            ["PAYMENT_PENDING"] = ["CONFIRMED", "CANCELLED"],
            ["CONFIRMED"] = ["PROCESSING", "CANCELLED"],
            ["PROCESSING"] = ["SHIPPED"], ["SHIPPED"] = ["DELIVERED"]
        };
        var next = request.Status?.Trim().ToUpperInvariant();
        if (next is null || !transitions.TryGetValue(order.Status, out var allowed) || !allowed.Contains(next))
            return Conflict(new { message = "This order status transition is not allowed.", currentStatus = order.Status });
        order.Status = next; order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { order.Id, order.OrderNumber, order.Status, order.UpdatedAt });
    }

    public sealed record StatusRequest(string Status);
}
