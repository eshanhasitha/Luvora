using InventoryService.Models;

namespace InventoryService.Tests;

public class InventoryTests
{
    [Fact]
    public void New_Inventory_Should_Have_Zero_Reserved_Stock()
    {
        var inventory = new Inventory
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            AvailableQuantity = 100,
            ReservedQuantity = 0,
            ReorderLevel = 20
        };

        Assert.Equal(100, inventory.AvailableQuantity);
        Assert.Equal(0, inventory.ReservedQuantity);
    }

    [Fact]
    public void Reserving_Stock_Should_Decrease_Available_Quantity()
    {
        var inventory = new Inventory
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            AvailableQuantity = 100,
            ReservedQuantity = 0,
            ReorderLevel = 20
        };

        var quantity = 5;

        inventory.AvailableQuantity -= quantity;
        inventory.ReservedQuantity += quantity;

        Assert.Equal(95, inventory.AvailableQuantity);
        Assert.Equal(5, inventory.ReservedQuantity);
    }

    [Fact]
    public void Releasing_Stock_Should_Return_It_To_Available()
    {
        var inventory = new Inventory
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            AvailableQuantity = 95,
            ReservedQuantity = 5,
            ReorderLevel = 20
        };

        var quantity = 5;

        inventory.ReservedQuantity -= quantity;
        inventory.AvailableQuantity += quantity;

        Assert.Equal(100, inventory.AvailableQuantity);
        Assert.Equal(0, inventory.ReservedQuantity);
    }

    [Fact]
    public void Total_Quantity_Should_Equal_Available_Plus_Reserved()
    {
        var inventory = new Inventory
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            AvailableQuantity = 80,
            ReservedQuantity = 20,
            ReorderLevel = 20
        };

        var total =
            inventory.AvailableQuantity +
            inventory.ReservedQuantity;

        Assert.Equal(100, total);
    }

    [Fact]
    public void Inventory_Should_Be_Low_Stock_When_Below_Reorder_Level()
    {
        var inventory = new Inventory
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            AvailableQuantity = 10,
            ReservedQuantity = 0,
            ReorderLevel = 20
        };

        var isLowStock =
            inventory.AvailableQuantity <=
            inventory.ReorderLevel;

        Assert.True(isLowStock);
    }

    [Fact]
    public void Inventory_Should_Not_Be_Low_Stock_When_Above_Reorder_Level()
    {
        var inventory = new Inventory
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            AvailableQuantity = 50,
            ReservedQuantity = 0,
            ReorderLevel = 20
        };

        var isLowStock =
            inventory.AvailableQuantity <=
            inventory.ReorderLevel;

        Assert.False(isLowStock);
    }
}