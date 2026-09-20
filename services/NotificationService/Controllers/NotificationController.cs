using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.DTOs;
using NotificationService.Models;
using NotificationService.Templates;

namespace NotificationService.Controllers;

[ApiController]
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
        var notifications =
            await _dbContext.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

        return Ok(notifications);
    }

    [HttpGet("{notificationId:guid}")]
    public async Task<IActionResult> GetById(
        Guid notificationId)
    {
        var notification =
            await _dbContext.Notifications
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    n => n.Id == notificationId);

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
        var notification =
            await _dbContext.Notifications
                .FirstOrDefaultAsync(
                    n => n.Id == notificationId);

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

    [HttpPost("order-created")]
    public async Task<IActionResult> CreateOrderCreatedNotification(
        [FromQuery] Guid userId,
        [FromQuery] string orderNumber)
    {
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

}