using AuthService.Constants;
using AuthService.Data;
using AuthService.Models;
using AuthService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Controllers;

[ApiController]
[Route("api/admin/staff")]
public sealed class AdminStaffController(AuthDbContext db, PasswordService passwords) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] bool? active, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Users.AsNoTracking().Where(x => x.Role != Roles.Customer && x.Role != Roles.DeliveryAgent).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Email.Contains(search) || x.FirstName.Contains(search) || x.LastName.Contains(search));
        if (active.HasValue) query = query.Where(x => x.IsActive == active);
        var total = await query.CountAsync();
        var staff = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.FirstName, x.LastName, x.Email, x.Role, x.IsActive, x.CreatedAt }).ToListAsync();
        return Ok(new { page, pageSize, totalItems = total, staff });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateStaffRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 12)
            return BadRequest(new { message = "First name, last name, email, and a password of at least 12 characters are required." });
        var email = request.Email.Trim().ToLowerInvariant();
        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email)) return BadRequest(new { message = "A valid email address is required." });
        if (await db.Users.AnyAsync(x => x.Email == email)) return Conflict(new { message = "An account with this email already exists." });
        if (request.Role is not (Roles.Staff or Roles.Admin)) return BadRequest(new { message = "Staff role must be Staff or Admin." });
        var user = new User { Id = Guid.NewGuid(), FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), Email = email,
            PasswordHash = passwords.HashPassword(request.Password), Role = request.Role, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Users.Add(user); await db.SaveChangesAsync();
        return Created($"/api/admin/staff/{user.Id}", new { user.Id, user.FirstName, user.LastName, user.Email, user.Role, user.IsActive });
    }

    [HttpGet("{staffId:guid}")]
    public async Task<IActionResult> Get(Guid staffId)
    {
        var user = await db.Users.AsNoTracking().Where(x => x.Id == staffId && x.Role != Roles.Customer && x.Role != Roles.DeliveryAgent)
            .Select(x => new { x.Id, x.FirstName, x.LastName, x.Email, x.Role, x.IsActive, x.CreatedAt }).SingleOrDefaultAsync();
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPatch("{staffId:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid staffId, StatusRequest request)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == staffId && x.Role != Roles.Customer && x.Role != Roles.DeliveryAgent);
        if (user is null) return NotFound();
        user.IsActive = request.Active; user.UpdatedAt = DateTime.UtcNow;
        if (!user.IsActive)
        {
            var activeTokens = await db.RefreshTokens.Where(x => x.UserId == user.Id && !x.IsRevoked).ToListAsync();
            foreach (var token in activeTokens) token.RevokedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(); return Ok(new { user.Id, user.IsActive, user.UpdatedAt });
    }

    [HttpPut("{staffId:guid}/roles")]
    public async Task<IActionResult> SetRole(Guid staffId, RoleRequest request)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == staffId && x.Role != Roles.Customer && x.Role != Roles.DeliveryAgent);
        if (user is null) return NotFound();
        if (request.Role is not (Roles.Staff or Roles.Admin)) return BadRequest(new { message = "Role must be Staff or Admin." });
        user.Role = request.Role; user.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync();
        return Ok(new { user.Id, user.Role, user.UpdatedAt });
    }

    [HttpGet("{staffId:guid}/roles")]
    public async Task<IActionResult> GetRole(Guid staffId)
    {
        var user = await db.Users.AsNoTracking().Where(x => x.Id == staffId && x.Role != Roles.Customer && x.Role != Roles.DeliveryAgent)
            .Select(x => new { x.Id, x.Role }).SingleOrDefaultAsync();
        return user is null ? NotFound() : Ok(new { staffId = user.Id, roles = new[] { user.Role } });
    }

    public sealed record CreateStaffRequest(string FirstName, string LastName, string Email, string Password, string Role = Roles.Staff);
    public sealed record StatusRequest(bool Active);
    public sealed record RoleRequest(string Role);
}
