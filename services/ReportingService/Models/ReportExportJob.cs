namespace ReportingService.Models;

public sealed class ReportExportJob
{
    public Guid Id { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string Status { get; set; } = "Queued";
    public string? Error { get; set; }
    public string? FileName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DownloadExpiresAt { get; set; }
}
