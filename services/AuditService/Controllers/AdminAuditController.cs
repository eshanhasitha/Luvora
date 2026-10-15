using AuditService.Data;
using AuditService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuditService.Controllers;

[ApiController]
[Route("api/admin/audit-logs")]
public sealed class AdminAuditController(AuditDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid? actorUserId, [FromQuery] string? category, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200); var query = db.Records.AsNoTracking().AsQueryable();
        if (actorUserId.HasValue) query = query.Where(x => x.ActorUserId == actorUserId);
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(x => x.Category == category);
        if (from.HasValue) query = query.Where(x => x.OccurredAt >= from);
        if (to.HasValue) query = query.Where(x => x.OccurredAt < to.Value.AddDays(1));
        var count = await query.CountAsync(); return Ok(new { page, pageSize, totalItems = count, records = await query.OrderByDescending(x => x.OccurredAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync() });
    }

    [HttpGet("{logId:guid}")]
    public async Task<IActionResult> Get(Guid logId)
    {
        var item = await db.Records.AsNoTracking().SingleOrDefaultAsync(x => x.Id == logId);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("security-events")]
    public async Task<IActionResult> SecurityEvents([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200); var query = db.SecurityEvents.AsNoTracking().AsQueryable();
        if (from.HasValue) query = query.Where(x => x.OccurredAt >= from);
        if (to.HasValue) query = query.Where(x => x.OccurredAt < to.Value.AddDays(1));
        return Ok(await query.OrderByDescending(x => x.OccurredAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync());
    }

    [HttpGet("staff-activity")]
    public async Task<IActionResult> StaffActivity([FromQuery] Guid? staffId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        var query = db.Records.AsNoTracking().Where(x => x.ActorRole == "Admin" || x.ActorRole == "Staff");
        if (staffId.HasValue) query = query.Where(x => x.ActorUserId == staffId);
        return Ok(await query.OrderByDescending(x => x.OccurredAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync());
    }

    [HttpGet("exports")]
    public async Task<IActionResult> ExportHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        return Ok(await db.Records.AsNoTracking().Where(x => x.Category == "export").OrderByDescending(x => x.OccurredAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync());
    }
}

[ApiController]
[Route("api/internal/audit")]
public sealed class AuditIngestController(AuditDbContext db, IConfiguration configuration) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Append(AuditInput input)
    {
        var expected = configuration["Audit:IngestKey"];
        if (string.IsNullOrWhiteSpace(expected) || !string.Equals(Request.Headers["X-Audit-Key"], expected, StringComparison.Ordinal)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(input.Action) || input.Action.Length > 120 || input.Resource.Length > 500) return BadRequest();
        var category = input.Category ?? "admin";
        db.Records.Add(new AuditRecord { Id = Guid.NewGuid(), ActorUserId = input.ActorUserId, ActorRole = input.ActorRole ?? string.Empty,
            Category = category, Action = input.Action, Resource = input.Resource, HttpMethod = input.HttpMethod ?? string.Empty,
            ResponseStatus = input.ResponseStatus, CorrelationId = input.CorrelationId ?? string.Empty, OccurredAt = DateTime.UtcNow });
        if (category == "security") db.SecurityEvents.Add(new SecurityEvent { Id = Guid.NewGuid(), UserId = input.ActorUserId,
            EventType = $"AdminEndpoint:{input.Action}:{input.ResponseStatus}", Source = input.Resource,
            CorrelationId = input.CorrelationId ?? string.Empty, OccurredAt = DateTime.UtcNow });
        await db.SaveChangesAsync(); return Accepted();
    }

    public sealed record AuditInput(Guid? ActorUserId, string? ActorRole, string Action, string Resource, string? HttpMethod, int? ResponseStatus, string? CorrelationId, string? Category = "admin");
}

[ApiController]
[Route("api/admin/system")]
public sealed class AdminSystemController(AuditDbContext db, IConfiguration config, IHttpClientFactory clients, IWebHostEnvironment environment) : ControllerBase
{
    private static readonly string[] ServiceKeys = ["Auth", "Product", "Inventory", "Order", "Payment", "Delivery", "Promotion", "Support", "Configuration", "Reporting", "Audit", "Notification", "Review"];

    [HttpGet("health")]
    public async Task<IActionResult> Health()
    {
        var connected = await db.Database.CanConnectAsync();
        return StatusCode(connected ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable,
            new { status = connected ? "Healthy" : "Degraded", service = "AuditService", environment = environment.EnvironmentName, timestamp = DateTime.UtcNow });
    }

    [HttpGet("services")]
    public async Task<IActionResult> Services(CancellationToken cancellationToken)
    {
        var result = new List<object>(); var client = clients.CreateClient("health");
        foreach (var key in ServiceKeys)
        {
            var address = config[$"ServiceUrls:{key}"];
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)) { result.Add(new { service = key, status = "Unknown" }); continue; }
            try { using var response = await client.GetAsync(new Uri(uri, "health"), cancellationToken); result.Add(new { service = key, status = response.IsSuccessStatusCode ? "Healthy" : "Unhealthy" }); }
            catch (HttpRequestException) { result.Add(new { service = key, status = "Unhealthy" }); }
            catch (TaskCanceledException) { result.Add(new { service = key, status = "Unhealthy" }); }
        }
        return Ok(result);
    }

    [HttpGet("integrations")]
    public IActionResult Integrations() => Ok(config.GetSection("IntegrationStatus").GetChildren().Select(x => new { name = x.Key, status = x["Status"] ?? "Unknown" }));

    [HttpPost("integrations/{integrationId}/test")]
    public async Task<IActionResult> TestIntegration(string integrationId, CancellationToken cancellationToken)
    {
        var address = config[$"Integrations:{integrationId}:HealthUrl"];
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)) return NotFound();
        try { using var response = await clients.CreateClient("health").GetAsync(uri, cancellationToken); return Ok(new { integrationId, status = response.IsSuccessStatusCode ? "Healthy" : "Unhealthy" }); }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException) { return Ok(new { integrationId, status = "Unhealthy" }); }
    }

    [HttpGet("storage")]
    public IActionResult Storage()
    {
        var root = Path.GetPathRoot(environment.ContentRootPath) ?? environment.ContentRootPath;
        var drive = new DriveInfo(root);
        return Ok(new { totalBytes = drive.TotalSize, availableBytes = drive.AvailableFreeSpace });
    }

    [HttpGet("queues")]
    public IActionResult Queues() => Problem(statusCode: StatusCodes.Status501NotImplemented, title: "Queue metrics are not available from the configured broker.");

    [HttpGet("jobs/failed")]
    public IActionResult FailedJobs() => Problem(statusCode: StatusCodes.Status501NotImplemented, title: "No centralized failed-job store is configured.");

    [HttpPost("jobs/failed/{jobId}/retry")]
    public IActionResult RetryJob(string jobId) => Problem(statusCode: StatusCodes.Status501NotImplemented, title: "No centralized failed-job store is configured.");
}
