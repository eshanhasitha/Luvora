using AuthService.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Controllers;

[ApiController]
[Route("api/database")]
public class DatabaseController : ControllerBase
{
    private readonly AuthDbContext _dbContext;

    public DatabaseController(AuthDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("health")]
    public async Task<IActionResult> Health()
    {
        var canConnect = await _dbContext.Database.CanConnectAsync();

        return Ok(new
        {
            service = "AuthService",
            database = "auth_db",
            connected = canConnect
        });
    }
}