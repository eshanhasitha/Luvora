using System.Security.Claims;
using AuthService.Application.DTOs;
using AuthService.Constants;
using AuthService.Data;
using AuthService.DTOs;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Controllers;

[ApiController]
[Route("api/admin/auth")]
public sealed class AdminAuthController(AuthDbContext db, PasswordService passwords, TokenService tokens) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email && x.IsActive);
        if (user is null || user.Role != Roles.Admin || !passwords.VerifyPassword(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid admin credentials." });
        return Ok(new { accessToken = tokens.GenerateAccessToken(user), expiresInMinutes = 15, user = new { user.Id, user.FirstName, user.LastName, user.Email, user.Role } });
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized();
        var user = await db.Users.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Id, x.FirstName, x.LastName, x.Email, x.PhoneNumber, x.Role }).SingleOrDefaultAsync();
        return user is null ? Unauthorized() : Ok(user);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPatch("me")]
    public async Task<IActionResult> UpdateMe(UpdateProfileRequest request)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id);
        if (user is null) return Unauthorized();
        user.FirstName = request.FirstName.Trim(); user.LastName = request.LastName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { user.Id, user.FirstName, user.LastName, user.Email, user.PhoneNumber, user.Role });
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id);
        if (user is null || !passwords.VerifyPassword(request.CurrentPassword, user.PasswordHash)) return BadRequest(new { message = "Current password is incorrect." });
        user.PasswordHash = passwords.HashPassword(request.NewPassword); user.UpdatedAt = DateTime.UtcNow;
        var activeTokens = await db.RefreshTokens.Where(x => x.UserId == user.Id && !x.IsRevoked).ToListAsync();
        foreach (var token in activeTokens) token.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
