using System.Text.Json;
using ConfigurationService.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConfigurationService.Controllers;

[ApiController]
[Route("api/storefront")]
public sealed class StorefrontContentController(ConfigurationDbContext db) : ControllerBase
{
    [HttpGet("homepage")]
    public async Task<IActionResult> Homepage()
    {
        var configuration = await db.Configurations.AsNoTracking().SingleOrDefaultAsync(x => x.Section == "homepage");
        var values = configuration is null ? new Dictionary<string, JsonElement>()
            : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(configuration.ValuesJson) ?? [];
        return Ok(new { values });
    }
}
