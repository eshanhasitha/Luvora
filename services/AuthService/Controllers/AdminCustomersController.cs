using AuthService.Constants;
using AuthService.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Controllers;

[ApiController]
[Route("api/admin/customers")]
public sealed class AdminCustomersController(AuthDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] bool? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Users.AsNoTracking().Where(x => x.Role == Roles.Customer).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Email.Contains(search) || x.FirstName.Contains(search) || x.LastName.Contains(search));
        if (status.HasValue) query = query.Where(x => x.IsActive == status);
        var total = await query.CountAsync();
        var customers = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.FirstName, x.LastName, x.Email, x.PhoneNumber, x.IsActive, x.CreatedAt }).ToListAsync();
        return Ok(new { page, pageSize, totalItems = total, customers });
    }

    [HttpGet("{customerId:guid}")]
    public async Task<IActionResult> Get(Guid customerId)
    {
        var customer = await db.Users.AsNoTracking().Where(x => x.Id == customerId && x.Role == Roles.Customer)
            .Select(x => new { x.Id, x.FirstName, x.LastName, x.Email, x.PhoneNumber, x.IsActive, x.CreatedAt }).SingleOrDefaultAsync();
        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpPatch("{customerId:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid customerId, StatusRequest request)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == customerId && x.Role == Roles.Customer);
        if (user is null) return NotFound();
        user.IsActive = request.Active; user.UpdatedAt = DateTime.UtcNow;
        if (!user.IsActive)
        {
            var activeTokens = await db.RefreshTokens.Where(x => x.UserId == user.Id && !x.IsRevoked).ToListAsync();
            foreach (var token in activeTokens) token.RevokedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
        return Ok(new { user.Id, user.IsActive, user.UpdatedAt });
    }

    public sealed record StatusRequest(bool Active);
}
