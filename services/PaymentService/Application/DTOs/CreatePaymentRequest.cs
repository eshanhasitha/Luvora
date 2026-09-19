using System.ComponentModel.DataAnnotations;

namespace PaymentService.DTOs;

public class CreatePaymentRequest
{
    [Required]
    public Guid OrderId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required]
    public string PaymentMethod { get; set; } = string.Empty;
}