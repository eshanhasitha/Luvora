using Microsoft.AspNetCore.Mvc;
using ReportingService.Services;

namespace ReportingService.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
public sealed class AdminDashboardController(ReportDataProvider data) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        try
        {
            var sales = await data.GetAsync("sales", DateTime.UtcNow.Date.AddDays(-29), DateTime.UtcNow, ct);
            var stock = await data.GetAsync("inventory", null, null, ct);
            return Ok(new { sales, inventory = stock });
        }
        catch (HttpRequestException exception) { return Problem(statusCode: 503, title: exception.Message); }
    }

    [HttpGet("sales-chart")]
    public async Task<IActionResult> SalesChart([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) => await Data("sales", from, to, ct);
    [HttpGet("orders-chart")]
    public async Task<IActionResult> OrdersChart([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) => await Data("orders", from, to, ct);
    [HttpGet("recent-orders")]
    public async Task<IActionResult> RecentOrders(CancellationToken ct) => await Data("orders", DateTime.UtcNow.AddDays(-14), DateTime.UtcNow, ct);
    [HttpGet("top-products")]
    public async Task<IActionResult> TopProducts(CancellationToken ct) => await Data("products", DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, ct);
    [HttpGet("low-stock")]
    public async Task<IActionResult> LowStock(CancellationToken ct) => await Data("inventory", null, null, ct);
    [HttpGet("pending-tasks")]
    public async Task<IActionResult> PendingTasks(CancellationToken ct) => await Data("orders", null, null, ct);
    [HttpGet("revenue-summary")]
    public async Task<IActionResult> RevenueSummary([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) => await Data("revenue", from, to, ct);

    private async Task<IActionResult> Data(string type, DateTime? from, DateTime? to, CancellationToken ct)
    {
        try { return new JsonResult(await data.GetAsync(type, from, to, ct)); }
        catch (HttpRequestException exception) { return Problem(statusCode: 503, title: exception.Message); }
    }
}
