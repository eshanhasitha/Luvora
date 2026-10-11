namespace AuthService.Models;

public class UserPreference
{
    public Guid UserId { get; set; }
    public string Language { get; set; } = "en";
    public string Currency { get; set; } = "LKR";
    public string Theme { get; set; } = "system";
    public bool EmailNotifications { get; set; } = true;
    public bool SmsNotifications { get; set; }
    public bool MarketingEmails { get; set; }
    public DateTime UpdatedAt { get; set; }
}
