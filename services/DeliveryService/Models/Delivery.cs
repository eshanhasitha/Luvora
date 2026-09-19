namespace DeliveryService.Models;

public class Delivery
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Guid UserId { get; set; }

    public string Address { get; set; } = string.Empty;

    public string Status { get; set; } = "PENDING";

    public string TrackingNumber { get; set; } = string.Empty;

    public DateTime? ShippedAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}