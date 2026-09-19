using Microsoft.EntityFrameworkCore;
using PaymentService.Data;
using PaymentService.Models;

namespace PaymentService.Tests;

public class PaymentTests
{
    private static PaymentDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<PaymentDbContext>()
                .UseInMemoryDatabase(
                    Guid.NewGuid().ToString())
                .Options;

        return new PaymentDbContext(options);
    }

    [Fact]
    public async Task NewPayment_ShouldHavePendingStatus()
    {
        await using var db = CreateDbContext();

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 100m,
            PaymentMethod = "CARD",
            Status = "PENDING",
            TransactionId = "TXN-001",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Payments.Add(payment);

        await db.SaveChangesAsync();

        var saved =
            await db.Payments.FirstAsync();

        Assert.Equal("PENDING", saved.Status);
    }

    [Fact]
    public async Task Payment_ShouldStoreCorrectAmount()
    {
        await using var db = CreateDbContext();

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 249.99m,
            PaymentMethod = "CARD",
            Status = "COMPLETED",
            TransactionId = "TXN-002",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Payments.Add(payment);

        await db.SaveChangesAsync();

        var saved =
            await db.Payments.FirstAsync();

        Assert.Equal(249.99m, saved.Amount);
    }

    [Fact]
    public async Task CompletedPayment_ShouldHaveCompletedStatus()
    {
        await using var db = CreateDbContext();

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 50m,
            PaymentMethod = "CARD",
            Status = "COMPLETED",
            TransactionId = "TXN-003",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Payments.Add(payment);

        await db.SaveChangesAsync();

        var saved =
            await db.Payments.FirstAsync();

        Assert.Equal(
            "COMPLETED",
            saved.Status);
    }

    [Fact]
    public async Task Payment_ShouldStoreTransactionId()
    {
        await using var db = CreateDbContext();

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Amount = 75m,
            PaymentMethod = "CARD",
            Status = "COMPLETED",
            TransactionId = "TXN-TEST-123",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Payments.Add(payment);

        await db.SaveChangesAsync();

        var saved =
            await db.Payments.FirstAsync();

        Assert.Equal(
            "TXN-TEST-123",
            saved.TransactionId);
    }
}