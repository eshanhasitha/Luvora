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
        if (request.UserId is null || request.ProductId is null)
            return BadRequest(new { message = "UserId and ProductId are required for this review route." });

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
            IsApproved = true,
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
        var review =
            await _dbContext.Reviews
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    r => r.Id == reviewId);

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
    public async Task<IActionResult> DeleteReview(
        Guid reviewId)
    {
        var review =
            await _dbContext.Reviews
                .FirstOrDefaultAsync(
                    r => r.Id == reviewId);

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
}
