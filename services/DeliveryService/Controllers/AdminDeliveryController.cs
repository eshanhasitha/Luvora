using DeliveryService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliveryService.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/delivery")]
public sealed class AdminDeliveryController(DeliveryDbContext db) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List([FromQuery] string? status, [FromQuery] Guid? orderId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
        => Shipments(status, orderId, page, pageSize);

    [HttpGet("shipments/{shipmentId:guid}")]
    public async Task<IActionResult> Shipment(Guid shipmentId)
    {
        var shipment = await db.Deliveries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == shipmentId);
        return shipment is null ? NotFound() : Ok(shipment);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var rows = await db.Deliveries.AsNoTracking().ToListAsync();
        return Ok(new { total = rows.Count, pending = rows.Count(x => x.Status == "PENDING"), inTransit = rows.Count(x => x.Status is "PROCESSING" or "SHIPPED"), delivered = rows.Count(x => x.Status == "DELIVERED") });
    }

    [HttpGet("shipments")]
    public async Task<IActionResult> Shipments([FromQuery] string? status, [FromQuery] Guid? orderId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Deliveries.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (orderId.HasValue) query = query.Where(x => x.OrderId == orderId);
        var total = await query.CountAsync();
        return Ok(new { page, pageSize, totalItems = total, shipments = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync() });
    }

    [HttpPatch("shipments/{shipmentId:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid shipmentId, StatusRequest request)
    {
        var shipment = await db.Deliveries.SingleOrDefaultAsync(x => x.Id == shipmentId);
        if (shipment is null) return NotFound();
        var next = request.Status?.Trim().ToUpperInvariant();
        var allowed = shipment.Status switch
        {
            "PENDING" => new[] { "PROCESSING" },
            "PROCESSING" => new[] { "SHIPPED" },
            "SHIPPED" => new[] { "DELIVERED" },
            _ => Array.Empty<string>()
        };
        if (next is null || !allowed.Contains(next)) return Conflict(new { message = "This delivery status transition is not allowed.", currentStatus = shipment.Status });
        shipment.Status = next; shipment.UpdatedAt = DateTime.UtcNow;
        if (next == "SHIPPED") shipment.ShippedAt = DateTime.UtcNow;
        if (next == "DELIVERED") shipment.DeliveredAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { shipment.Id, shipment.Status, shipment.UpdatedAt });
    }

    public sealed record StatusRequest(string Status);
}
