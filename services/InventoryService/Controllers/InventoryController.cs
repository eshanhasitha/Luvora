using InventoryService.Data;
using InventoryService.DTOs;
using InventoryService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace InventoryService.Controllers;

[ApiController]
[Authorize]
[Route("api/inventory")]
public class InventoryController : ControllerBase
{
    private readonly InventoryDbContext _dbContext;

    public InventoryController(
        InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> Create(
        CreateInventoryRequest request)
    {
        var existingInventory =
            await _dbContext.Inventories
                .FirstOrDefaultAsync(
                    i => i.ProductId == request.ProductId);

        if (existingInventory != null)
        {
            return Conflict(new
            {
                message = "Inventory already exists for this product."
            });
        }

        var inventory = new Inventory
        {
            Id = Guid.NewGuid(),
            ProductId = request.ProductId,
            AvailableQuantity = request.AvailableQuantity,
            ReservedQuantity = 0,
            ReorderLevel = request.ReorderLevel,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Inventories.Add(inventory);

        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetByProductId),
            new { productId = inventory.ProductId },
            ToResponse(inventory)
        );
    }

    [HttpGet("{productId:guid}")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> GetByProductId(
        Guid productId)
    {
        var inventory = await _dbContext.Inventories
            .AsNoTracking()
            .FirstOrDefaultAsync(
                i => i.ProductId == productId);

        if (inventory == null)
        {
            return NotFound(new
            {
                message = "Inventory not found."
            });
        }

        return Ok(ToResponse(inventory));
    }

    [AllowAnonymous]
    [HttpGet("{productId:guid}/availability")]
    public async Task<IActionResult> Availability(Guid productId)
    {
        var inventory = await _dbContext.Inventories.AsNoTracking().FirstOrDefaultAsync(x => x.ProductId == productId && x.IsActive);
        return inventory is null ? Ok(new { productId, available = false, availableQuantity = 0 })
            : Ok(new { productId, available = inventory.AvailableQuantity > 0, availableQuantity = inventory.AvailableQuantity });
    }

    private static InventoryResponse ToResponse(
        Inventory inventory)
    {
        return new InventoryResponse
        {
            Id = inventory.Id,
            ProductId = inventory.ProductId,
            AvailableQuantity = inventory.AvailableQuantity,
            ReservedQuantity = inventory.ReservedQuantity,
            ReorderLevel = inventory.ReorderLevel,
            TotalQuantity =
                inventory.AvailableQuantity +
                inventory.ReservedQuantity,
            IsLowStock =
                inventory.AvailableQuantity <=
                inventory.ReorderLevel,
            IsActive = inventory.IsActive
        };
    }

    [HttpPost("reserve")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> ReserveStock(
        ReserveStockRequest request)
    {
        var inventory = await _dbContext.Inventories
            .FirstOrDefaultAsync(
                i => i.ProductId == request.ProductId);

        if (inventory == null)
        {
            return NotFound(new
            {
                message = "Inventory not found."
            });
        }

        if (!inventory.IsActive)
        {
            return BadRequest(new
            {
                message = "Inventory is inactive."
            });
        }

        if (inventory.AvailableQuantity < request.Quantity)
        {
            return Conflict(new
            {
                message = "Insufficient stock.",
                availableQuantity =
                    inventory.AvailableQuantity
            });
        }

        inventory.AvailableQuantity -= request.Quantity;

        inventory.ReservedQuantity += request.Quantity;

        inventory.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return Ok(ToResponse(inventory));
    }

    [HttpPost("release")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> ReleaseStock(
        ReleaseStockRequest request)
    {
        var inventory = await _dbContext.Inventories
            .FirstOrDefaultAsync(
                i => i.ProductId == request.ProductId);

        if (inventory == null)
        {
            return NotFound(new
            {
                message = "Inventory not found."
            });
        }

        if (inventory.ReservedQuantity < request.Quantity)
        {
            return Conflict(new
            {
                message = "Cannot release more stock than reserved.",
                reservedQuantity = inventory.ReservedQuantity
            });
        }

        inventory.ReservedQuantity -= request.Quantity;
        inventory.AvailableQuantity += request.Quantity;
        inventory.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return Ok(ToResponse(inventory));
    }

    [HttpPut("{productId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(
        Guid productId,
        UpdateInventoryRequest request)
    {
        return Conflict(new { message = "Direct stock quantity writes are disabled. Use the auditable admin adjustment endpoint." });
    }
}
