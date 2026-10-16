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
    [HttpGet("/api/users/me")]
    public async Task<IActionResult> GetProfile()
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == CurrentUserId);
        if (user is null) return Unauthorized();
        return Ok(new { id = user.Id, firstName = user.FirstName, lastName = user.LastName, email = user.Email, phoneNumber = user.PhoneNumber, dateOfBirth = user.DateOfBirth, displayName = user.DisplayName, wardrobeCategory = user.WardrobeCategory, standardSize = user.StandardSize, alterationPreference = user.AlterationPreference, preferredFabrics = user.PreferredFabrics, secondaryPhone = user.SecondaryPhone, preferredChannel = user.PreferredChannel, role = user.Role });
    }

    [HttpPut("profile")]
    [HttpPatch("/api/users/me")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request)
    {
        if (request.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
            return BadRequest(new { message = "Date of birth must be in the past." });

        if (!IsOneOf(request.WardrobeCategory, "women", "men", "unisex"))
            return BadRequest(new { message = "Wardrobe category must be women, men, or unisex." });

        if (!IsOneOf(request.PreferredChannel, "email", "phone", "whatsapp"))
            return BadRequest(new { message = "Preferred channel must be email, phone, or whatsapp." });

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == CurrentUserId);
        if (user is null) return Unauthorized();
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        user.DateOfBirth = request.DateOfBirth;
        user.DisplayName = Normalize(request.DisplayName);
        user.WardrobeCategory = Normalize(request.WardrobeCategory)?.ToLowerInvariant();
        user.StandardSize = Normalize(request.StandardSize);
        user.AlterationPreference = Normalize(request.AlterationPreference);
        user.PreferredFabrics = (request.PreferredFabrics ?? []).Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        user.SecondaryPhone = Normalize(request.SecondaryPhone);
        user.PreferredChannel = Normalize(request.PreferredChannel)?.ToLowerInvariant();
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return await GetProfile();
    }

    [HttpGet("addresses")]
    [HttpGet("/api/users/me/addresses")]
    public async Task<IActionResult> GetAddresses() => Ok(await db.UserAddresses.AsNoTracking()
        .Where(x => x.UserId == CurrentUserId).OrderByDescending(x => x.IsDefault).ThenBy(x => x.CreatedAt).ToListAsync());

    [HttpPost("addresses")]
    [HttpPost("/api/users/me/addresses")]
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
    [HttpPut("/api/users/me/addresses/{addressId:guid}")]
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

    [HttpPatch("/api/users/me/addresses/{addressId:guid}/default")]
    public async Task<IActionResult> SetDefaultAddress(Guid addressId)
    {
        var userId = CurrentUserId;
        var address = await db.UserAddresses.SingleOrDefaultAsync(x => x.Id == addressId && x.UserId == userId);
        if (address is null) return NotFound();
        await ClearDefaultAddress(userId);
        address.IsDefault = true;
        address.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { address.Id, address.IsDefault, address.UpdatedAt });
    }

    [HttpDelete("addresses/{addressId:guid}")]
    [HttpDelete("/api/users/me/addresses/{addressId:guid}")]
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
    [HttpGet("/api/users/me/preferences")]
    [HttpGet("/api/users/me/notifications/preferences")]
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
    [HttpPut("/api/users/me/preferences")]
    [HttpPut("/api/users/me/notifications/preferences")]
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
    [HttpPost("/api/users/me/account-deletion")]
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

    [HttpGet("/api/users/me/wishlist")]
    public async Task<IActionResult> GetWishlist()
    {
        var items = await db.WishlistItems.AsNoTracking().Where(x => x.UserId == CurrentUserId).OrderByDescending(x => x.CreatedAt).ToListAsync();
        return Ok(items);
    }

    [HttpPost("/api/users/me/wishlist/items")]
    public async Task<IActionResult> AddWishlistItem(WishlistRequest request)
    {
        if (request.ProductId == Guid.Empty) return BadRequest(new { message = "A product ID is required." });
        var userId = CurrentUserId;
        if (await db.WishlistItems.AnyAsync(x => x.UserId == userId && x.ProductId == request.ProductId)) return Conflict(new { message = "Product is already in the wishlist." });
        var item = new WishlistItem { Id = Guid.NewGuid(), UserId = userId, ProductId = request.ProductId, CreatedAt = DateTime.UtcNow };
        db.WishlistItems.Add(item); await db.SaveChangesAsync(); return Created($"/api/users/me/wishlist/items/{item.ProductId}", item);
    }

    [HttpDelete("/api/users/me/wishlist/items/{productId:guid}")]
    public async Task<IActionResult> RemoveWishlistItem(Guid productId)
    {
        var item = await db.WishlistItems.SingleOrDefaultAsync(x => x.UserId == CurrentUserId && x.ProductId == productId);
        if (item is null) return NotFound();
        db.WishlistItems.Remove(item); await db.SaveChangesAsync(); return NoContent();
    }

    [HttpDelete("/api/users/me/wishlist/items")]
    public async Task<IActionResult> ClearWishlist()
    {
        var items = await db.WishlistItems.Where(x => x.UserId == CurrentUserId).ToListAsync();
        db.WishlistItems.RemoveRange(items); await db.SaveChangesAsync(); return NoContent();
    }

    [HttpGet("/api/users/me/wishlist/items/{productId:guid}")]
    public async Task<IActionResult> IsInWishlist(Guid productId) => Ok(new { productId, saved = await db.WishlistItems.AnyAsync(x => x.UserId == CurrentUserId && x.ProductId == productId) });

    [HttpPost("/api/users/me/data-export")]
    [HttpPost("/api/users/me/privacy/data-export")]
    public IActionResult RequestDataExport() => Problem(statusCode: StatusCodes.Status501NotImplemented, title: "Personal data export is not configured.");

    [HttpGet("/api/users/me/privacy")]
    public IActionResult Privacy() => Ok(new { accountDeletion = true, dataExport = false, retentionPolicy = "Contact support for data requests." });

    public sealed record WishlistRequest(Guid ProductId);

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
        target.DeliveryInstructions = Normalize(source.DeliveryInstructions);
        target.IsDefault = source.IsDefault;
        return target;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsOneOf(string? value, params string[] allowed) =>
        string.IsNullOrWhiteSpace(value) || allowed.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);
}
