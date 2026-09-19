using System.ComponentModel.DataAnnotations;

namespace DeliveryService.DTOs;

public class CreateDeliveryRequest
{
    [Required]
    public Guid OrderId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Address { get; set; } = string.Empty;
}