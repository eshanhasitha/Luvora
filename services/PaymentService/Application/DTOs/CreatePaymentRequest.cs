using System.ComponentModel.DataAnnotations;

namespace PaymentService.DTOs;

public class CreatePaymentRequest
{
    [Required]
    public Guid OrderId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    // Ignored by PaymentService; the amount is loaded from the authenticated customer's order.
    public decimal Amount { get; set; }

    [Required]
    public string PaymentMethod { get; set; } = string.Empty;
}
