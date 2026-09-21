using OrderService.Events;
using NotificationService.Templates;

namespace Messaging.Tests;

public class MessagingTests
{
    [Fact]
    public void OrderCreatedEvent_ShouldContainOrderId()
    {
        var orderId = Guid.NewGuid();

        var eventMessage = new OrderCreatedEvent
        {
            OrderId = orderId,
            UserId = Guid.NewGuid(),
            TotalAmount = 199.99m,
            CreatedAt = DateTime.UtcNow
        };

        Assert.Equal(
            orderId,
            eventMessage.OrderId);
    }

    [Fact]
    public void OrderCreatedEvent_ShouldContainUserId()
    {
        var userId = Guid.NewGuid();

        var eventMessage = new OrderCreatedEvent
        {
            OrderId = Guid.NewGuid(),
            UserId = userId,
            TotalAmount = 100m,
            CreatedAt = DateTime.UtcNow
        };

        Assert.Equal(
            userId,
            eventMessage.UserId);
    }

    [Fact]
    public void OrderCreatedEvent_ShouldContainTotalAmount()
    {
        var eventMessage = new OrderCreatedEvent
        {
            OrderId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TotalAmount = 499.99m,
            CreatedAt = DateTime.UtcNow
        };

        Assert.Equal(
            499.99m,
            eventMessage.TotalAmount);
    }

    [Fact]
    public void OrderCreatedTemplate_ShouldContainOrderId()
    {
        var template =
            NotificationTemplates.CreateOrderCreated(
                "LUV-1001");

        Assert.Contains(
            "LUV-1001",
            template.Message);
    }

    [Fact]
    public void PaymentCompletedTemplate_ShouldContainOrderId()
    {
        var template =
            NotificationTemplates.CreatePaymentCompleted(
                "LUV-1001",
                250m);

        Assert.Contains(
            "LUV-1001",
            template.Message);
    }

    [Fact]
    public void OrderShippedTemplate_ShouldContainTrackingNumber()
    {
        var template =
            NotificationTemplates.CreateOrderShipped(
                "LUV-1001",
                "TRACK-123");

        Assert.Contains(
            "TRACK-123",
            template.Message);
    }

    [Fact]
    public void OrderDeliveredTemplate_ShouldContainOrderNumber()
    {
        var template =
            NotificationTemplates.CreateOrderDelivered(
                "LUV-1001");

        Assert.Contains(
            "LUV-1001",
            template.Message);
    }

    [Fact]
    public void OrderCancelledTemplate_ShouldContainOrderNumber()
    {
        var template =
            NotificationTemplates.CreateOrderCancelled(
                "LUV-1001");

        Assert.Contains(
            "LUV-1001",
            template.Message);
    }
}