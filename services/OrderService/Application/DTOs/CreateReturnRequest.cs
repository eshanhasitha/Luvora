using System.ComponentModel.DataAnnotations;

namespace OrderService.DTOs;

public class CreateReturnRequest
{
    [Required]
    public Guid OrderItemId { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; }

    [Required, MinLength(2), MaxLength(200)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Details { get; set; }
}
