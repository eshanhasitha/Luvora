using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;

namespace NotificationService.Controllers;

[ApiController]
[Route("api/database")]
public class DatabaseController : ControllerBase
{
    private readonly NotificationDbContext _dbContext;

    public DatabaseController(NotificationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("health")]
    public async Task<IActionResult> Health()
    {
        var canConnect = await _dbContext.Database.CanConnectAsync();

        return Ok(new
        {
            service = "NotificationService",
            database = "notification_db",
            connected = canConnect
        });
    }
}