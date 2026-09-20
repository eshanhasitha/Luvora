using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReviewService.Data;
using ReviewService.DTOs;
using ReviewService.Models;

namespace ReviewService.Controllers;

[ApiController]
[Route("api/reviews")]
public class ReviewController : ControllerBase
{
    private readonly ReviewDbContext _dbContext;

    public ReviewController(ReviewDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost]
    public async Task<IActionResult> CreateReview(
        CreateReviewRequest request)
    {
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
            UserId = request.UserId,
            ProductId = request.ProductId,
            OrderId = request.OrderId,
            Rating = request.Rating,
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
            Comment = review.Comment,
            IsApproved = review.IsApproved,
            CreatedAt = review.CreatedAt
        };
    }
}