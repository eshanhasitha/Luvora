using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.DTOs;
using NotificationService.Models;
using NotificationService.Templates;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace NotificationService.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationController : ControllerBase
{
    private readonly NotificationDbContext _dbContext;

    public NotificationController(
        NotificationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateNotificationRequest request)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Staff")) return Forbid();
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Type = request.Type.Trim(),
            Title = request.Title.Trim(),
            Message = request.Message.Trim(),
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Notifications.Add(notification);

        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { notificationId = notification.Id },
            notification
        );
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<IActionResult> GetUserNotifications(
        Guid userId)
    {
        if (!Owns(userId)) return Forbid();
        var notifications =
            await _dbContext.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

        return Ok(notifications);
    }

    [HttpGet("/api/users/me/notifications")]
    public Task<IActionResult> GetMyNotifications() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? GetUserNotifications(id) : Task.FromResult<IActionResult>(Unauthorized());

    [HttpGet("{notificationId:guid}")]
    public async Task<IActionResult> GetById(
        Guid notificationId)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("Staff");
        var notification =
            await _dbContext.Notifications
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    n => n.Id == notificationId && (n.UserId == userId || isAdmin));

        if (notification == null)
        {
            return NotFound(new
            {
                message = "Notification not found."
            });
        }

        return Ok(notification);
    }

    [HttpPut("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(
        Guid notificationId)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var notification =
            await _dbContext.Notifications
                .FirstOrDefaultAsync(
                    n => n.Id == notificationId && n.UserId == userId);

        if (notification == null)
        {
            return NotFound(new
            {
                message = "Notification not found."
            });
        }

        notification.IsRead = true;

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Notification marked as read."
        });
    }

    [HttpGet("user/{userId:guid}/unread-count")]
    public async Task<IActionResult> GetUnreadCount(
        Guid userId)
    {
        if (!Owns(userId)) return Forbid();
        var count =
            await _dbContext.Notifications
                .CountAsync(n =>
                    n.UserId == userId &&
                    !n.IsRead);

        return Ok(new
        {
            unreadCount = count
        });
    }

    [HttpGet("/api/users/me/notifications/unread-count")]
    public Task<IActionResult> GetMyUnreadCount() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? GetUnreadCount(id) : Task.FromResult<IActionResult>(Unauthorized());

    [HttpPatch("/api/users/me/notifications/{notificationId:guid}/read")]
    public Task<IActionResult> MarkMyNotificationRead(Guid notificationId) => MarkAsRead(notificationId);

    [HttpPost("/api/users/me/notifications/read-all")]
    public async Task<IActionResult> MarkMyNotificationsRead()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var items = await _dbContext.Notifications.Where(x => x.UserId == userId && !x.IsRead).ToListAsync();
        foreach (var item in items) item.IsRead = true;
        await _dbContext.SaveChangesAsync(); return NoContent();
    }

    [HttpDelete("/api/users/me/notifications/{notificationId:guid}")]
    public async Task<IActionResult> DeleteMyNotification(Guid notificationId)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var item = await _dbContext.Notifications.SingleOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId);
        if (item is null) return NotFound();
        _dbContext.Notifications.Remove(item); await _dbContext.SaveChangesAsync(); return NoContent();
    }

    [HttpPost("order-created")]
    public async Task<IActionResult> CreateOrderCreatedNotification(
        [FromQuery] Guid userId,
        [FromQuery] string orderNumber)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Staff")) return Forbid();
        var template =
            NotificationTemplates.CreateOrderCreated(
                orderNumber);

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = "ORDER_CREATED",
            Title = template.Title,
            Message = template.Message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Notifications.Add(notification);

        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { notificationId = notification.Id },
            notification
        );
    }

    private bool Owns(Guid userId) => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId) && currentUserId == userId;

}
