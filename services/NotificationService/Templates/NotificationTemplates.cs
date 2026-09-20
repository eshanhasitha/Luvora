namespace NotificationService.Templates;

public static class NotificationTemplates
{
    public static (
        string Title,
        string Message)
        CreateOrderCreated(string orderNumber)
    {
        return (
            "Order Created",
            $"Your order {orderNumber} has been created successfully."
        );
    }

    public static (
        string Title,
        string Message)
        CreatePaymentCompleted(
            string orderNumber,
            decimal amount)
    {
        return (
            "Payment Successful",
            $"Payment of {amount:C} for order {orderNumber} was completed successfully."
        );
    }

    public static (
        string Title,
        string Message)
        CreateOrderShipped(
            string orderNumber,
            string trackingNumber)
    {
        return (
            "Order Shipped",
            $"Your order {orderNumber} has been shipped. Tracking number: {trackingNumber}."
        );
    }

    public static (
        string Title,
        string Message)
        CreateOrderDelivered(
            string orderNumber)
    {
        return (
            "Order Delivered",
            $"Your order {orderNumber} has been delivered successfully."
        );
    }

    public static (
        string Title,
        string Message)
        CreateOrderCancelled(
            string orderNumber)
    {
        return (
            "Order Cancelled",
            $"Your order {orderNumber} has been cancelled."
        );
    }
}