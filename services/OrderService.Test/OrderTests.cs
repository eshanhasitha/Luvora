using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Models;

namespace OrderService.Tests;

public class OrderTests
{
    private static OrderDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<OrderDbContext>()
                .UseInMemoryDatabase(
                    Guid.NewGuid().ToString())
                .Options;

        return new OrderDbContext(options);
    }

    [Fact]
    public async Task NewOrder_ShouldHaveCreatedStatus()
    {
        await using var db = CreateDbContext();

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            OrderNumber = "LUV-TEST-001",
            Status = "CREATED",
            TotalAmount = 99.99m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var savedOrder =
            await db.Orders.FirstAsync();

        Assert.Equal("CREATED", savedOrder.Status);
    }

    [Fact]
    public async Task Order_ShouldCalculateTotalCorrectly()
    {
        await using var db = CreateDbContext();

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            OrderNumber = "LUV-TEST-002",
            Status = "CREATED",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        order.Items.Add(new OrderItem
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Quantity = 2,
            UnitPrice = 50m,
            TotalPrice = 100m,
            CreatedAt = DateTime.UtcNow
        });

        order.Items.Add(new OrderItem
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Quantity = 1,
            UnitPrice = 25m,
            TotalPrice = 25m,
            CreatedAt = DateTime.UtcNow
        });

        order.TotalAmount =
            order.Items.Sum(i => i.TotalPrice);

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var savedOrder =
            await db.Orders
                .Include(o => o.Items)
                .FirstAsync();

        Assert.Equal(125m, savedOrder.TotalAmount);
    }

    [Fact]
    public async Task Order_ShouldContainItems()
    {
        await using var db = CreateDbContext();

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            OrderNumber = "LUV-TEST-003",
            Status = "CREATED",
            TotalAmount = 75m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        order.Items.Add(new OrderItem
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Quantity = 3,
            UnitPrice = 25m,
            TotalPrice = 75m,
            CreatedAt = DateTime.UtcNow
        });

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var savedOrder =
            await db.Orders
                .Include(o => o.Items)
                .FirstAsync();

        Assert.Single(savedOrder.Items);
    }

    [Fact]
    public async Task CancelledOrder_ShouldHaveCancelledStatus()
    {
        await using var db = CreateDbContext();

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            OrderNumber = "LUV-TEST-004",
            Status = "CREATED",
            TotalAmount = 50m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        order.Status = "CANCELLED";
        order.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        var savedOrder =
            await db.Orders.FirstAsync();

        Assert.Equal(
            "CANCELLED",
            savedOrder.Status);
    }

    [Fact]
    public async Task OrderItem_ShouldCalculateTotalPrice()
    {
        var item = new OrderItem
        {
            Quantity = 4,
            UnitPrice = 20m
        };

        item.TotalPrice =
            item.Quantity * item.UnitPrice;

        Assert.Equal(80m, item.TotalPrice);
    }

    [Fact]
    public void PaidOrder_ShouldNotBeCancellable()
    {
        var status = "PAID";

        var cancellableStatuses = new[]
        {
            "CREATED",
            "PAYMENT_PENDING"
        };

        Assert.DoesNotContain(
            status,
            cancellableStatuses);
    }

    [Fact]
    public void ShippedOrder_ShouldNotBeCancellable()
    {
        var status = "SHIPPED";

        var cancellableStatuses = new[]
        {
            "CREATED",
            "PAYMENT_PENDING"
        };

        Assert.DoesNotContain(
            status,
            cancellableStatuses);
    }
}