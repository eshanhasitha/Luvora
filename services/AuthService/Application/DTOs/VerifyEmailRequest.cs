using System.ComponentModel.DataAnnotations;

namespace AuthService.Application.DTOs;

public sealed class VerifyEmailRequest
{
    [Required, MaxLength(256)]
    public string Token { get; set; } = string.Empty;
}
