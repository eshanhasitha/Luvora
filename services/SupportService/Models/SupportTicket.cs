namespace SupportService.Models;

public sealed class SupportTicket
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? AssignedStaffId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal";
    public string Status { get; set; } = "Open";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
