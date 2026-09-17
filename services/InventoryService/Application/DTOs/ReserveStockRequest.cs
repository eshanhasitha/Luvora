using System.ComponentModel.DataAnnotations;

namespace InventoryService.DTOs;

public class ReserveStockRequest
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}