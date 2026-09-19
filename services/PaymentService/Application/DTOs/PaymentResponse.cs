namespace PaymentService.DTOs;

public class PaymentResponse
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Guid UserId { get; set; }

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string TransactionId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}