using System.Security.Claims;
using AuthService.Application.DTOs;
using AuthService.Data;
using AuthService.Models;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Controllers;

[ApiController]
[Authorize]
[Route("api/account")]
public class AccountController(AuthDbContext db, PasswordService passwords) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == CurrentUserId);
        if (user is null) return Unauthorized();
        return Ok(new { id = user.Id, firstName = user.FirstName, lastName = user.LastName, email = user.Email, phoneNumber = user.PhoneNumber, role = user.Role });
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == CurrentUserId);
        if (user is null) return Unauthorized();
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return await GetProfile();
    }

    [HttpGet("addresses")]
    public async Task<IActionResult> GetAddresses() => Ok(await db.UserAddresses.AsNoTracking()
        .Where(x => x.UserId == CurrentUserId).OrderByDescending(x => x.IsDefault).ThenBy(x => x.CreatedAt).ToListAsync());

    [HttpPost("addresses")]
    public async Task<IActionResult> CreateAddress(AddressRequest request)
    {
        var userId = CurrentUserId;
        var hasAddresses = await db.UserAddresses.AnyAsync(x => x.UserId == userId);
        var now = DateTime.UtcNow;
        var address = Apply(new UserAddress { Id = Guid.NewGuid(), UserId = userId, CreatedAt = now, UpdatedAt = now }, request);
        address.IsDefault = request.IsDefault || !hasAddresses;
        if (address.IsDefault) await ClearDefaultAddress(userId);
        db.UserAddresses.Add(address);
        await db.SaveChangesAsync();
        return Created($"/api/account/addresses/{address.Id}", address);
    }

    [HttpPut("addresses/{addressId:guid}")]
    public async Task<IActionResult> UpdateAddress(Guid addressId, AddressRequest request)
    {
        var userId = CurrentUserId;
        var address = await db.UserAddresses.FirstOrDefaultAsync(x => x.Id == addressId && x.UserId == userId);
        if (address is null) return NotFound();
        Apply(address, request);
        address.UpdatedAt = DateTime.UtcNow;
        if (request.IsDefault)
        {
            await ClearDefaultAddress(userId);
            address.IsDefault = true;
        }
        await db.SaveChangesAsync();
        return Ok(address);
    }

    [HttpDelete("addresses/{addressId:guid}")]
    public async Task<IActionResult> DeleteAddress(Guid addressId)
    {
        var address = await db.UserAddresses.FirstOrDefaultAsync(x => x.Id == addressId && x.UserId == CurrentUserId);
        if (address is null) return NotFound();
        var wasDefault = address.IsDefault;
        db.UserAddresses.Remove(address);
        if (wasDefault)
        {
            var replacement = await db.UserAddresses.Where(x => x.UserId == CurrentUserId && x.Id != addressId).OrderBy(x => x.CreatedAt).FirstOrDefaultAsync();
            if (replacement is not null) replacement.IsDefault = true;
        }
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences()
    {
        var userId = CurrentUserId;
        var preferences = await db.UserPreferences.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
        if (preferences is null)
        {
            preferences = new UserPreference { UserId = userId, UpdatedAt = DateTime.UtcNow };
            db.UserPreferences.Add(preferences);
            await db.SaveChangesAsync();
        }
        return Ok(preferences);
    }

    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences(UpdatePreferencesRequest request)
    {
        var userId = CurrentUserId;
        var preferences = await db.UserPreferences.FirstOrDefaultAsync(x => x.UserId == userId);
        if (preferences is null)
        {
            preferences = new UserPreference { UserId = userId };
            db.UserPreferences.Add(preferences);
        }
        preferences.Language = request.Language.Trim();
        preferences.Currency = request.Currency.Trim().ToUpperInvariant();
        preferences.Theme = request.Theme.Trim().ToLowerInvariant();
        preferences.EmailNotifications = request.EmailNotifications;
        preferences.SmsNotifications = request.SmsNotifications;
        preferences.MarketingEmails = request.MarketingEmails;
        preferences.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(preferences);
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAccount(DeleteAccountRequest request)
    {
        if (!request.Confirm) return BadRequest(new { message = "Explicit account deletion confirmation is required." });
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == CurrentUserId);
        if (user is null) return Unauthorized();
        if (!passwords.VerifyPassword(request.Password, user.PasswordHash)) return BadRequest(new { message = "Password is incorrect." });
        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        var tokens = await db.RefreshTokens.Where(x => x.UserId == user.Id && !x.IsRevoked).ToListAsync();
        foreach (var token in tokens) token.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { message = "Account deactivated successfully." });
    }

    private async Task ClearDefaultAddress(Guid userId)
    {
        var defaults = await db.UserAddresses.Where(x => x.UserId == userId && x.IsDefault).ToListAsync();
        foreach (var item in defaults) item.IsDefault = false;
    }

    private static UserAddress Apply(UserAddress target, AddressRequest source)
    {
        target.Label = source.Label.Trim();
        target.RecipientName = source.RecipientName.Trim();
        target.PhoneNumber = source.PhoneNumber.Trim();
        target.AddressLine1 = source.AddressLine1.Trim();
        target.AddressLine2 = string.IsNullOrWhiteSpace(source.AddressLine2) ? null : source.AddressLine2.Trim();
        target.City = source.City.Trim();
        target.Region = string.IsNullOrWhiteSpace(source.Region) ? null : source.Region.Trim();
        target.PostalCode = string.IsNullOrWhiteSpace(source.PostalCode) ? null : source.PostalCode.Trim();
        target.Country = source.Country.Trim();
        target.IsDefault = source.IsDefault;
        return target;
    }
}
