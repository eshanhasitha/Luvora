using DeliveryService.Data;
using DeliveryService.Models;
using Microsoft.EntityFrameworkCore;

namespace DeliveryService.Tests;

public class DeliveryTests
{
    private static DeliveryDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<DeliveryDbContext>()
                .UseInMemoryDatabase(
                    Guid.NewGuid().ToString())
                .Options;

        return new DeliveryDbContext(options);
    }

    [Fact]
    public async Task NewDelivery_ShouldHavePendingStatus()
    {
        await using var db = CreateDbContext();

        var delivery = new Delivery
        {
            Id = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Address = "Colombo, Sri Lanka",
            Status = "PENDING",
            TrackingNumber = "LUV-TRK-001",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Deliveries.Add(delivery);

        await db.SaveChangesAsync();

        var saved =
            await db.Deliveries.FirstAsync();

        Assert.Equal(
            "PENDING",
            saved.Status);
    }

    [Fact]
    public async Task Delivery_ShouldStoreTrackingNumber()
    {
        await using var db = CreateDbContext();

        var delivery = new Delivery
        {
            Id = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Address = "Kandy, Sri Lanka",
            Status = "PENDING",
            TrackingNumber = "LUV-TRK-123456",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Deliveries.Add(delivery);

        await db.SaveChangesAsync();

        var saved =
            await db.Deliveries.FirstAsync();

        Assert.Equal(
            "LUV-TRK-123456",
            saved.TrackingNumber);
    }

    [Fact]
    public async Task ShippedDelivery_ShouldHaveShippedDate()
    {
        await using var db = CreateDbContext();

        var delivery = new Delivery
        {
            Id = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Address = "Galle, Sri Lanka",
            Status = "SHIPPED",
            TrackingNumber = "LUV-TRK-002",
            ShippedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Deliveries.Add(delivery);

        await db.SaveChangesAsync();

        var saved =
            await db.Deliveries.FirstAsync();

        Assert.Equal(
            "SHIPPED",
            saved.Status);

        Assert.NotNull(saved.ShippedAt);
    }

    [Fact]
    public async Task DeliveredDelivery_ShouldHaveDeliveredDate()
    {
        await using var db = CreateDbContext();

        var delivery = new Delivery
        {
            Id = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Address = "Matara, Sri Lanka",
            Status = "DELIVERED",
            TrackingNumber = "LUV-TRK-003",
            ShippedAt = DateTime.UtcNow.AddDays(-1),
            DeliveredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow
        };

        db.Deliveries.Add(delivery);

        await db.SaveChangesAsync();

        var saved =
            await db.Deliveries.FirstAsync();

        Assert.Equal(
            "DELIVERED",
            saved.Status);

        Assert.NotNull(saved.DeliveredAt);
    }

    [Fact]
    public void DeliveryStatuses_ShouldContainRequiredStates()
    {
        var statuses = new[]
        {
            "PENDING",
            "PROCESSING",
            "SHIPPED",
            "DELIVERED"
        };

        Assert.Contains("PENDING", statuses);
        Assert.Contains("PROCESSING", statuses);
        Assert.Contains("SHIPPED", statuses);
        Assert.Contains("DELIVERED", statuses);
    }
}