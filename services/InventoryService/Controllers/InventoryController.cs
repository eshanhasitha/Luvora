using InventoryService.Data;
using InventoryService.DTOs;
using InventoryService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Controllers;

[ApiController]
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
    public async Task<IActionResult> Update(
        Guid productId,
        UpdateInventoryRequest request)
    {
        var inventory = await _dbContext.Inventories
            .FirstOrDefaultAsync(
                i => i.ProductId == productId);

        if (inventory == null)
        {
            return NotFound(new
            {
                message = "Inventory not found."
            });
        }

        inventory.AvailableQuantity =
            request.AvailableQuantity;

        inventory.ReorderLevel =
            request.ReorderLevel;

        inventory.IsActive =
            request.IsActive;

        inventory.UpdatedAt =
            DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return Ok(ToResponse(inventory));
    }
}