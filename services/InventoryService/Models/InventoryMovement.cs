namespace InventoryService.Models;

public sealed class InventoryMovement
{
    public Guid Id { get; set; }
    public Guid InventoryId { get; set; }
    public Guid ProductId { get; set; }
    public int QuantityDelta { get; set; }
    public int QuantityBefore { get; set; }
    public int QuantityAfter { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public DateTime CreatedAt { get; set; }
}
