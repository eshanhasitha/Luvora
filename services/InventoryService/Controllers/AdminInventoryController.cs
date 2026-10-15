using InventoryService.Data;
using InventoryService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Controllers;

[ApiController]
[Route("api/admin/inventory")]
public sealed class AdminInventoryController(InventoryDbContext db) : ControllerBase
{
    [HttpGet("stock/{productId:guid}")]
    public async Task<IActionResult> StockForProduct(Guid productId)
    {
        var row = await db.Inventories.AsNoTracking().SingleOrDefaultAsync(x => x.ProductId == productId);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var rows = await db.Inventories.AsNoTracking().Where(x => x.IsActive).ToListAsync();
        return Ok(new { productCount = rows.Count, available = rows.Sum(x => x.AvailableQuantity), reserved = rows.Sum(x => x.ReservedQuantity), lowStockCount = rows.Count(x => x.AvailableQuantity <= x.ReorderLevel), outOfStockCount = rows.Count(x => x.AvailableQuantity == 0) });
    }

    [HttpGet("low-stock")]
    public async Task<IActionResult> LowStock([FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        await Stock(null, true, page, pageSize);

    [HttpGet("out-of-stock")]
    public async Task<IActionResult> OutOfStock([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Inventories.AsNoTracking().Where(x => x.IsActive && x.AvailableQuantity == 0);
        var total = await query.CountAsync();
        return Ok(new { page, pageSize, totalItems = total, items = await query.OrderBy(x => x.ProductId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync() });
    }

    [HttpPost("adjustments")]
    public async Task<IActionResult> Adjust(AdjustmentRequest request)
    {
        if (request.QuantityDelta == 0 || string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { message = "A non-zero quantity delta and reason are required." });
        var inventory = await db.Inventories.SingleOrDefaultAsync(x => x.ProductId == request.ProductId && x.IsActive);
        if (inventory is null) return NotFound();
        var after = inventory.AvailableQuantity + request.QuantityDelta;
        if (after < 0) return Conflict(new { message = "Adjustment would make available stock negative." });
        var before = inventory.AvailableQuantity;
        inventory.AvailableQuantity = after;
        inventory.UpdatedAt = DateTime.UtcNow;
        var movement = new InventoryMovement { Id = Guid.NewGuid(), InventoryId = inventory.Id, ProductId = inventory.ProductId, QuantityDelta = request.QuantityDelta, QuantityBefore = before, QuantityAfter = after, Reason = request.Reason.Trim(), Reference = request.Reference?.Trim(), CreatedAt = DateTime.UtcNow };
        db.Movements.Add(movement);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Movements), new { productId = request.ProductId }, movement);
    }

    [HttpGet("adjustments")]
    public Task<IActionResult> Adjustments([FromQuery] Guid? productId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) => Movements(productId, page, pageSize);

    [HttpGet("movements")]
    public async Task<IActionResult> Movements([FromQuery] Guid? productId = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Movements.AsNoTracking().AsQueryable();
        if (productId.HasValue) query = query.Where(x => x.ProductId == productId.Value);
        var total = await query.CountAsync();
        return Ok(new { page, pageSize, totalItems = total, items = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync() });
    }

    [HttpGet("stock")]
    public async Task<IActionResult> Stock([FromQuery] Guid? productId, [FromQuery] bool lowStock = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Inventories.AsNoTracking().Where(x => x.IsActive).AsQueryable();
        if (productId.HasValue) query = query.Where(x => x.ProductId == productId);
        if (lowStock) query = query.Where(x => x.AvailableQuantity <= x.ReorderLevel);
        var total = await query.CountAsync();
        return Ok(new { page, pageSize, totalItems = total, items = await query.OrderBy(x => x.ProductId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync() });
    }
}

public sealed record AdjustmentRequest(Guid ProductId, int QuantityDelta, string Reason, string? Reference);
