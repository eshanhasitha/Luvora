namespace SupportService.Models;

public sealed class Complaint
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? OrderId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
