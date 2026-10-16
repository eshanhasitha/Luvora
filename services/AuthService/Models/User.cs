using AuthService.Constants;

namespace AuthService.Models;

public class User
{
    public Guid Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? DisplayName { get; set; }

    public string? WardrobeCategory { get; set; }

    public string? StandardSize { get; set; }

    public string? AlterationPreference { get; set; }

    public string[] PreferredFabrics { get; set; } = [];

    public string? SecondaryPhone { get; set; }

    public string? PreferredChannel { get; set; }

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = Roles.Customer;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
