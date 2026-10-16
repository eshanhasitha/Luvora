using System.ComponentModel.DataAnnotations;

namespace AuthService.Application.DTOs;

public class UpdateProfileRequest
{
    [Required, MaxLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string LastName { get; set; } = string.Empty;
    [Phone, MaxLength(40)] public string? PhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    [MaxLength(120)] public string? DisplayName { get; set; }
    [MaxLength(20)] public string? WardrobeCategory { get; set; }
    [MaxLength(30)] public string? StandardSize { get; set; }
    [MaxLength(500)] public string? AlterationPreference { get; set; }
    [MaxLength(20)] public string[]? PreferredFabrics { get; set; }
    [Phone, MaxLength(40)] public string? SecondaryPhone { get; set; }
    [MaxLength(20)] public string? PreferredChannel { get; set; }
}

public class AddressRequest
{
    [MaxLength(80)] public string Label { get; set; } = "Home";
    [Required, MaxLength(200)] public string RecipientName { get; set; } = string.Empty;
    [Required, Phone, MaxLength(40)] public string PhoneNumber { get; set; } = string.Empty;
    [Required, MaxLength(250)] public string AddressLine1 { get; set; } = string.Empty;
    [MaxLength(250)] public string? AddressLine2 { get; set; }
    [Required, MaxLength(120)] public string City { get; set; } = string.Empty;
    [MaxLength(120)] public string? Region { get; set; }
    [MaxLength(30)] public string? PostalCode { get; set; }
    [Required, MaxLength(120)] public string Country { get; set; } = string.Empty;
    [MaxLength(500)] public string? DeliveryInstructions { get; set; }
    public bool IsDefault { get; set; }
}

public class UpdatePreferencesRequest
{
    [Required, MaxLength(20)] public string Language { get; set; } = "en";
    [Required, MaxLength(10)] public string Currency { get; set; } = "LKR";
    [Required, MaxLength(20)] public string Theme { get; set; } = "system";
    public bool EmailNotifications { get; set; }
    public bool SmsNotifications { get; set; }
    public bool MarketingEmails { get; set; }
}

public class DeleteAccountRequest
{
    [Required] public string Password { get; set; } = string.Empty;
    [Required] public bool Confirm { get; set; }
}
