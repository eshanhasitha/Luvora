using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CartService.Data;

namespace CartService.Controllers;

[ApiController]
[Route("api/database")]
public class DatabaseController : ControllerBase
{
    private readonly CartDbContext _dbContext;

    public DatabaseController(CartDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("health")]
    public async Task<IActionResult> Health()
    {
        var canConnect = await _dbContext.Database.CanConnectAsync();

        return Ok(new
        {
            service = "CartService",
            database = "cart_db",
            connected = canConnect
        });
    }
}