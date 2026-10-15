namespace ReviewService.Models;

public sealed class ReviewReport
{
    public Guid Id { get; set; }
    public Guid ReviewId { get; set; }
    public Guid ReporterUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
