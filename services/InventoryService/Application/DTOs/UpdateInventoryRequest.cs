using System.ComponentModel.DataAnnotations;

namespace InventoryService.DTOs;

public class UpdateInventoryRequest
{
    [Range(0, int.MaxValue)]
    public int AvailableQuantity { get; set; }

    [Range(0, int.MaxValue)]
    public int ReorderLevel { get; set; }

    public bool IsActive { get; set; }
}