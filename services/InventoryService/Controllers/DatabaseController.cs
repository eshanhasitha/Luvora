using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventoryService.Data;

namespace InventoryService.Controllers;

[ApiController]
[Route("api/database")]
public class DatabaseController : ControllerBase
{
    private readonly InventoryDbContext _dbContext;

    public DatabaseController(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("health")]
    public async Task<IActionResult> Health()
    {
        var canConnect = await _dbContext.Database.CanConnectAsync();

        return Ok(new
        {
            service = "InventoryService",
            database = "inventory_db",
            connected = canConnect
        });
    }
}