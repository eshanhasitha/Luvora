namespace ConfigurationService.Models;

public sealed class StoreConfiguration
{
    public Guid Id { get; set; }
    public string Section { get; set; } = string.Empty;
    public string ValuesJson { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }
}
