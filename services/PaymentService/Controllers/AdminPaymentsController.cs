using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaymentService.Data;

namespace PaymentService.Controllers;

[ApiController]
[Route("api/admin/payments")]
public sealed class AdminPaymentsController(PaymentDbContext db) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List([FromQuery] string? status, [FromQuery] Guid? orderId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
        => Transactions(status, orderId, from, to, page, pageSize);

    [HttpGet("transactions/{transactionId:guid}")]
    public async Task<IActionResult> Transaction(Guid transactionId)
    {
        var payment = await db.Payments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == transactionId);
        return payment is null ? NotFound() : Ok(payment);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var items = await db.Payments.AsNoTracking().ToListAsync();
        return Ok(new { count = items.Count, totalAmount = items.Where(x => x.Status == "COMPLETED").Sum(x => x.Amount), byStatus = items.GroupBy(x => x.Status).Select(g => new { status = g.Key, count = g.Count(), amount = g.Sum(x => x.Amount) }) });
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> Transactions([FromQuery] string? status, [FromQuery] Guid? orderId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Payments.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (orderId.HasValue) query = query.Where(x => x.OrderId == orderId);
        if (from.HasValue) query = query.Where(x => x.CreatedAt >= from);
        if (to.HasValue) query = query.Where(x => x.CreatedAt < to.Value.AddDays(1));
        var total = await query.CountAsync();
        return Ok(new { page, pageSize, totalItems = total, transactions = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync() });
    }

    [HttpGet("orders/{orderId:guid}/transactions")]
    public async Task<IActionResult> ForOrder(Guid orderId)
        => Ok(await db.Payments.AsNoTracking().Where(x => x.OrderId == orderId).OrderByDescending(x => x.CreatedAt).ToListAsync());
}
