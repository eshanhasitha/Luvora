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
}