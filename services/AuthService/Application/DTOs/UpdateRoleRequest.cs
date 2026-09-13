using System.ComponentModel.DataAnnotations;

namespace AuthService.DTOs;

public class UpdateRoleRequest
{
    [Required]
    public string Role { get; set; } = string.Empty;
}