using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;

namespace NotificationService.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/notifications")]
public sealed class AdminNotificationsController(NotificationDbContext db) : ControllerBase
{
    [HttpGet("{notificationId:guid}")]
    public async Task<IActionResult> Get(Guid notificationId)
    {
        var notification = await db.Notifications.AsNoTracking().SingleOrDefaultAsync(x => x.Id == notificationId);
        return notification is null ? NotFound() : Ok(notification);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool? unread, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Notifications.AsNoTracking().AsQueryable();
        if (unread == true) query = query.Where(x => !x.IsRead);
        var total = await query.CountAsync();
        return Ok(new { page, pageSize, totalItems = total, notifications = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync() });
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount() => Ok(new { unreadCount = await db.Notifications.CountAsync(x => !x.IsRead) });

    [HttpPatch("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId)
    {
        var notification = await db.Notifications.SingleOrDefaultAsync(x => x.Id == notificationId);
        if (notification is null) return NotFound();
        notification.IsRead = true; await db.SaveChangesAsync(); return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll()
    {
        await db.Notifications.Where(x => !x.IsRead).ExecuteUpdateAsync(update => update.SetProperty(x => x.IsRead, true));
        return NoContent();
    }
}
