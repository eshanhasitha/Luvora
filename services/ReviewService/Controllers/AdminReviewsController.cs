using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReviewService.Data;

namespace ReviewService.Controllers;

[ApiController]
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

    public sealed record StatusRequest(string Status);
}
