using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ReviewService.Data;

namespace ReviewService.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/reviews")]
public sealed class AdminReviewsController(ReviewDbContext db) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary()
    {
        var total = await db.Reviews.CountAsync();
        var approved = await db.Reviews.CountAsync(x => x.IsApproved);
        return Ok(new { total, approved, pending = total - approved, averageRating = await db.Reviews.Select(x => (double?)x.Rating).AverageAsync() ?? 0 });
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool? approved, [FromQuery] Guid? productId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Reviews.AsNoTracking().AsQueryable();
        if (approved.HasValue) query = query.Where(x => x.IsApproved == approved);
        if (productId.HasValue) query = query.Where(x => x.ProductId == productId);
        var total = await query.CountAsync();
        return Ok(new { page, pageSize, totalItems = total, reviews = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync() });
    }

    [HttpGet("{reviewId:guid}")]
    public async Task<IActionResult> Get(Guid reviewId)
    {
        var review = await db.Reviews.AsNoTracking().SingleOrDefaultAsync(x => x.Id == reviewId);
        return review is null ? NotFound() : Ok(review);
    }

    [HttpPatch("{reviewId:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid reviewId, StatusRequest request)
    {
        var review = await db.Reviews.SingleOrDefaultAsync(x => x.Id == reviewId);
        if (review is null) return NotFound();
        var status = request.Status?.Trim().ToLowerInvariant();
        if (status is not ("approved" or "hidden" or "rejected")) return BadRequest(new { message = "Status must be approved, hidden, or rejected." });
        review.IsApproved = status == "approved"; review.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { review.Id, status, review.UpdatedAt });
    }

    [HttpPost("{reviewId:guid}/reply")]
    public async Task<IActionResult> Reply(Guid reviewId, ReplyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message)) return BadRequest(new { message = "Reply is required." });
        var review = await db.Reviews.SingleOrDefaultAsync(x => x.Id == reviewId);
        if (review is null) return NotFound();
        review.AdminReply = request.Message.Trim(); review.AdminReplyAt = DateTime.UtcNow; review.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(); return Ok(new { review.Id, review.AdminReply, review.AdminReplyAt });
    }

    [HttpGet("reported")]
    public async Task<IActionResult> Reported([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.ReviewReports.AsNoTracking().Where(x => x.Status == "Open");
        var total = await query.CountAsync();
        return Ok(new { totalItems = total, reports = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync() });
    }

    [HttpPost("{reviewId:guid}/resolve-report")]
    public async Task<IActionResult> ResolveReports(Guid reviewId, ResolveReportRequest request)
    {
        if (request.Resolution is not ("Resolved" or "Rejected")) return BadRequest(new { message = "Resolution must be Resolved or Rejected." });
        var reports = await db.ReviewReports.Where(x => x.ReviewId == reviewId && x.Status == "Open").ToListAsync();
        if (reports.Count == 0) return NotFound();
        foreach (var report in reports) { report.Status = request.Resolution; report.ResolvedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync(); return Ok(new { reviewId, resolvedCount = reports.Count, status = request.Resolution });
    }

    public sealed record StatusRequest(string Status);
    public sealed record ReplyRequest(string Message);
    public sealed record ResolveReportRequest(string Resolution);
}
