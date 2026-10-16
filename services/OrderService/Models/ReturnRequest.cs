namespace OrderService.Models;

public class ReturnRequest
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid OrderId { get; set; }
    public Guid OrderItemId { get; set; }
    public int Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string Status { get; set; } = "REQUESTED";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
