using System.ComponentModel.DataAnnotations;

namespace InventoryService.DTOs;

public class CreateInventoryRequest
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(0, int.MaxValue)]
    public int AvailableQuantity { get; set; }

    [Range(0, int.MaxValue)]
    public int ReorderLevel { get; set; }
}