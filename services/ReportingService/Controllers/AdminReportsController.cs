using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportingService.Data;
using ReportingService.Models;
using ReportingService.Services;

namespace ReportingService.Controllers;

[ApiController]
[Route("api/admin/reports")]
public sealed class AdminReportsController(ReportDataProvider data, ReportingDbContext db, IConfiguration configuration) : ControllerBase
{
    [HttpGet("sales")]
    public Task<IActionResult> Sales([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) => Get("sales", from, to, ct);
    [HttpGet("revenue")]
    public Task<IActionResult> Revenue([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) => Get("revenue", from, to, ct);
    [HttpGet("orders")]
    public Task<IActionResult> Orders([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) => Get("orders", from, to, ct);
    [HttpGet("products")]
    public Task<IActionResult> Products([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) => Get("products", from, to, ct);
    [HttpGet("inventory")]
    public Task<IActionResult> Inventory(CancellationToken ct) => Get("inventory", null, null, ct);
    [HttpGet("customers")]
    public Task<IActionResult> Customers([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) => Get("customers", from, to, ct);
    [HttpGet("payments")]
    public Task<IActionResult> Payments([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) => Get("payments", from, to, ct);
    [HttpGet("refunds")]
    public IActionResult Refunds() => Problem(statusCode: 501, title: "Refund data is not available because PaymentService has no refund records.");
    [HttpGet("delivery")]
    public Task<IActionResult> Delivery(CancellationToken ct) => Get("delivery", null, null, ct);
    [HttpGet("coupons")]
    public Task<IActionResult> Coupons(CancellationToken ct) => Get("coupons", null, null, ct);

    [HttpPost("exports")]
    public async Task<IActionResult> RequestExport(ExportRequest request)
    {
        var type = request.ReportType.Trim().ToLowerInvariant();
        if (!ReportDataProvider.SupportedReports.Contains(type)) return BadRequest(new { message = "Unsupported report type." });
        if (request.From.HasValue && request.To.HasValue && request.To < request.From) return BadRequest(new { message = "The to date must not precede the from date." });
        var job = new ReportExportJob { Id = Guid.NewGuid(), ReportType = type, From = request.From, To = request.To, Status = "Queued", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.ExportJobs.Add(job); await db.SaveChangesAsync();
        return Accepted($"/api/admin/reports/exports/{job.Id}", new { job.Id, job.Status, job.CreatedAt });
    }

    [HttpGet("exports")]
    public async Task<IActionResult> ListExports([FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100); var query = db.ExportJobs.AsNoTracking();
        return Ok(new { page, pageSize, exports = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new { x.Id, x.ReportType, x.Status, x.Error, x.CreatedAt, x.UpdatedAt, x.DownloadExpiresAt }).ToListAsync() });
    }

    [HttpGet("exports/{exportId:guid}")]
    public async Task<IActionResult> GetExport(Guid exportId)
    {
        var item = await db.ExportJobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == exportId);
        return item is null ? NotFound() : Ok(new { item.Id, item.ReportType, item.Status, item.Error, item.CreatedAt, item.UpdatedAt, item.DownloadExpiresAt, downloadUrl = item.Status == "Completed" ? $"/api/admin/reports/exports/{item.Id}/download" : null });
    }

    [HttpGet("exports/{exportId:guid}/download")]
    public async Task<IActionResult> Download(Guid exportId)
    {
        var job = await db.ExportJobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == exportId);
        if (job is null) return NotFound();
        if (job.Status != "Completed" || job.FileName is null || job.DownloadExpiresAt <= DateTime.UtcNow) return Conflict(new { message = "Export is unavailable or its temporary download period has expired." });
        var folder = configuration["Exports:Path"] ?? Path.Combine(AppContext.BaseDirectory, "exports");
        var path = Path.Combine(folder, job.FileName);
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, "application/json", $"{job.ReportType}-{job.Id:N}.json");
    }

    private async Task<IActionResult> Get(string type, DateTime? from, DateTime? to, CancellationToken ct)
    {
        if (from.HasValue && to.HasValue && to < from) return BadRequest(new { message = "The to date must not precede the from date." });
        try { return new JsonResult(await data.GetAsync(type, from, to, ct)); }
        catch (NotSupportedException exception) { return Problem(statusCode: 501, title: exception.Message); }
        catch (HttpRequestException exception) { return Problem(statusCode: 503, title: exception.Message); }
    }

    public sealed record ExportRequest(string ReportType, DateTime? From = null, DateTime? To = null);
}
