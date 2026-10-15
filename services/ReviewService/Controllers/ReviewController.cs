using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReviewService.Data;
using ReviewService.DTOs;
using ReviewService.Models;
using Microsoft.AspNetCore.Authorization;
using System.Net.Http.Json;
using System.Security.Claims;

namespace ReviewService.Controllers;

[ApiController]
[Route("api/reviews")]
public class ReviewController : ControllerBase
{
    private readonly ReviewDbContext _dbContext;
    private readonly HttpClient _productsClient;

    public ReviewController(ReviewDbContext dbContext, IHttpClientFactory clients)
    {
        _dbContext = dbContext;
        _productsClient = clients.CreateClient("products");
    }

    [HttpPost]
    public async Task<IActionResult> CreateReview(
        CreateReviewRequest request)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var authenticatedUserId)) return Unauthorized();
        request.UserId = authenticatedUserId;
        if (request.ProductId is null) return BadRequest(new { message = "ProductId is required for this review route." });

        if (request.Rating < 1 ||
            request.Rating > 5)
        {
            return BadRequest(new
            {
                message =
                    "Rating must be between 1 and 5."
            });
        }

        var existingReview =
            await _dbContext.Reviews
                .FirstOrDefaultAsync(r =>
                    r.UserId == request.UserId &&
                    r.ProductId == request.ProductId &&
                    r.OrderId == request.OrderId);

        if (existingReview != null)
        {
            return Conflict(new
            {
                message =
                    "You have already reviewed this product for this order."
            });
        }

        var review = new Review
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId!.Value,
            ProductId = request.ProductId!.Value,
            OrderId = request.OrderId,
            Rating = request.Rating,
            Title = request.Title.Trim(),
            Comment = request.Comment.Trim(),
            IsApproved = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Reviews.Add(review);

        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetReview),
            new { reviewId = review.Id },
            ToResponse(review)
        );
    }

    [HttpGet("{reviewId:guid}")]
    public async Task<IActionResult> GetReview(
        Guid reviewId)
    {
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId);
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("Staff");
        var review =
            await _dbContext.Reviews
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    r => r.Id == reviewId && (r.IsApproved || r.UserId == currentUserId || isAdmin));

        if (review == null)
        {
            return NotFound(new
            {
                message = "Review not found."
            });
        }

        return Ok(ToResponse(review));
    }

    [HttpGet("product/{productId:guid}")]
    public async Task<IActionResult> GetProductReviews(
        Guid productId)
    {
        var reviews =
            await _dbContext.Reviews
                .AsNoTracking()
                .Where(r =>
                    r.ProductId == productId &&
                    r.IsApproved)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

        return Ok(
            reviews.Select(ToResponse).ToList()
        );
    }

    [HttpGet("/api/products/{productId:guid}/reviews")]
    public Task<IActionResult> GetProductReviewsFromProductRoute(Guid productId)
    {
        return GetProductReviews(productId);
    }

    [HttpPost("/api/products/{productId:guid}/reviews")]
    public async Task<IActionResult> CreateReviewFromProductRoute(
        Guid productId,
        CreateReviewRequest request)
    {
        request.ProductId = productId;
        return await CreateReview(request);
    }

    [HttpGet("/api/products/{slug}/reviews")]
    public async Task<IActionResult> GetProductReviewsFromSlug(string slug)
    {
        var productId = await ResolveProductId(slug);
        return productId is null ? NotFound(new { message = "Product not found." }) : await GetProductReviews(productId.Value);
    }

    [Authorize]
    [HttpPost("/api/products/{slug}/reviews")]
    public async Task<IActionResult> CreateReviewFromSlug(string slug, CreateReviewRequest request)
    {
        var productId = await ResolveProductId(slug);
        if (productId is null) return NotFound(new { message = "Product not found." });

        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId)) return Unauthorized();

        request.UserId = userId;
        request.ProductId = productId.Value;
        request.OrderId = null;

        var duplicate = await _dbContext.Reviews.AnyAsync(r => r.UserId == userId && r.ProductId == productId.Value && r.OrderId == null);
        if (duplicate) return Conflict(new { message = "You have already reviewed this product." });
        return await CreateReview(request);
    }

    [HttpDelete("{reviewId:guid}")]
    [HttpDelete("/api/users/me/reviews/{reviewId:guid}")]
    public async Task<IActionResult> DeleteReview(
        Guid reviewId)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var authenticatedUserId)) return Unauthorized();
        var review =
            await _dbContext.Reviews
                .FirstOrDefaultAsync(
                    r => r.Id == reviewId && r.UserId == authenticatedUserId);

        if (review == null)
        {
            return NotFound(new
            {
                message = "Review not found."
            });
        }

        _dbContext.Reviews.Remove(review);

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Review deleted successfully."
        });
    }

    [Authorize]
    [HttpPost("{reviewId:guid}/report")]
    public async Task<IActionResult> ReportReview(Guid reviewId, ReportReviewRequest request)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Reason)) return BadRequest(new { message = "A report reason is required." });
        if (!await _dbContext.Reviews.AnyAsync(x => x.Id == reviewId && x.IsApproved)) return NotFound();
        if (await _dbContext.ReviewReports.AnyAsync(x => x.ReviewId == reviewId && x.ReporterUserId == userId && x.Status == "Open")) return Conflict(new { message = "You already have an open report for this review." });
        var report = new ReviewReport { Id = Guid.NewGuid(), ReviewId = reviewId, ReporterUserId = userId, Reason = request.Reason.Trim(), CreatedAt = DateTime.UtcNow };
        _dbContext.ReviewReports.Add(report); await _dbContext.SaveChangesAsync(); return Accepted(new { report.Id, report.Status, report.CreatedAt });
    }

    [Authorize]
    [HttpPatch("/api/users/me/reviews/{reviewId:guid}")]
    public async Task<IActionResult> UpdateMyReview(Guid reviewId, UpdateReviewRequest request)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        if (request.Rating is < 1 or > 5 || string.IsNullOrWhiteSpace(request.Comment)) return BadRequest(new { message = "Rating must be 1–5 and comment is required." });
        var review = await _dbContext.Reviews.SingleOrDefaultAsync(x => x.Id == reviewId && x.UserId == userId);
        if (review is null) return NotFound();
        review.Rating = request.Rating;
        review.Title = request.Title?.Trim() ?? string.Empty;
        review.Comment = request.Comment.Trim();
        review.IsApproved = false;
        review.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        return Ok(ToResponse(review));
    }

    [Authorize]
    [HttpGet("/api/users/me/reviews")]
    [HttpGet("me")]
    public Task<IActionResult> GetMyReviewsAlias() => GetMyReviews();

    private async Task<IActionResult> GetMyReviews()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var reviews = await _dbContext.Reviews.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).ToListAsync();
        return Ok(reviews.Select(ToResponse));
    }

    private static ReviewResponse ToResponse(
        Review review)
    {
        return new ReviewResponse
        {
            Id = review.Id,
            UserId = review.UserId,
            ProductId = review.ProductId,
            OrderId = review.OrderId,
            Rating = review.Rating,
            Title = review.Title,
            Comment = review.Comment,
            IsApproved = review.IsApproved,
            AdminReply = review.AdminReply,
            CreatedAt = review.CreatedAt
        };
    }

    private async Task<Guid?> ResolveProductId(string slug)
    {
        try
        {
            using var response = await _productsClient.GetAsync($"api/products/slug/{Uri.EscapeDataString(slug)}");
            if (!response.IsSuccessStatusCode) return null;
            var product = await response.Content.ReadFromJsonAsync<ProductLookup>();
            return product?.Id;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private sealed class ProductLookup
    {
        public Guid Id { get; set; }
    }

    public sealed record UpdateReviewRequest(int Rating, string Comment, string? Title = null);
    public sealed record ReportReviewRequest(string Reason);
}
