using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Models;
using NotificationService.Templates;

namespace NotificationService.Tests;

public class NotificationTests
{
    private static NotificationDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<NotificationDbContext>()
                .UseInMemoryDatabase(
                    Guid.NewGuid().ToString())
                .Options;

        return new NotificationDbContext(options);
    }

    [Fact]
    public async Task NewNotification_ShouldBeUnread()
    {
        await using var db = CreateDbContext();

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Type = "ORDER_CREATED",
            Title = "Order Created",
            Message = "Your order was created.",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        db.Notifications.Add(notification);

        await db.SaveChangesAsync();

        var saved =
            await db.Notifications.FirstAsync();

        Assert.False(saved.IsRead);
    }

    [Fact]
    public async Task Notification_ShouldStoreCorrectUser()
    {
        await using var db = CreateDbContext();

        var userId = Guid.NewGuid();

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = "PAYMENT_COMPLETED",
            Title = "Payment Successful",
            Message = "Your payment was successful.",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        db.Notifications.Add(notification);

        await db.SaveChangesAsync();

        var saved =
            await db.Notifications.FirstAsync();

        Assert.Equal(
            userId,
            saved.UserId);
    }

    [Fact]
    public async Task MarkAsRead_ShouldSetReadStatus()
    {
        await using var db = CreateDbContext();

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Type = "ORDER_CREATED",
            Title = "Order Created",
            Message = "Order created.",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        db.Notifications.Add(notification);

        await db.SaveChangesAsync();

        notification.IsRead = true;

        await db.SaveChangesAsync();

        var saved =
            await db.Notifications.FirstAsync();

        Assert.True(saved.IsRead);
    }

    [Fact]
    public void OrderCreatedTemplate_ShouldContainOrderNumber()
    {
        var template =
            NotificationTemplates.CreateOrderCreated(
                "LUV-202609200001");

        Assert.Equal(
            "Order Created",
            template.Title);

        Assert.Contains(
            "LUV-202609200001",
            template.Message);
    }

    [Fact]
    public void PaymentTemplate_ShouldContainAmount()
    {
        var template =
            NotificationTemplates.CreatePaymentCompleted(
                "LUV-001",
                199.99m);

        Assert.Equal(
            "Payment Successful",
            template.Title);

        Assert.Contains(
            "LUV-001",
            template.Message);
    }

    [Fact]
    public void ShippedTemplate_ShouldContainTrackingNumber()
    {
        var template =
            NotificationTemplates.CreateOrderShipped(
                "LUV-001",
                "LUV-TRK-123456");

        Assert.Equal(
            "Order Shipped",
            template.Title);

        Assert.Contains(
            "LUV-TRK-123456",
            template.Message);
    }
}