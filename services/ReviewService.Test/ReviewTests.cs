using Microsoft.EntityFrameworkCore;
using ReviewService.Data;
using ReviewService.Models;

namespace ReviewService.Tests;

public class ReviewTests
{
    private static ReviewDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<ReviewDbContext>()
                .UseInMemoryDatabase(
                    Guid.NewGuid().ToString())
                .Options;

        return new ReviewDbContext(options);
    }

    [Fact]
    public async Task NewReview_ShouldStoreCorrectRating()
    {
        await using var db = CreateDbContext();

        var review = new Review
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            Rating = 5,
            Comment = "Excellent product.",
            IsApproved = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Reviews.Add(review);

        await db.SaveChangesAsync();

        var saved =
            await db.Reviews.FirstAsync();

        Assert.Equal(5, saved.Rating);
    }

    [Fact]
    public async Task Review_ShouldStoreComment()
    {
        await using var db = CreateDbContext();

        var review = new Review
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            Rating = 4,
            Comment = "Good quality.",
            IsApproved = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Reviews.Add(review);

        await db.SaveChangesAsync();

        var saved =
            await db.Reviews.FirstAsync();

        Assert.Equal(
            "Good quality.",
            saved.Comment);
    }

    [Fact]
    public async Task Review_ShouldBeApprovedByDefault()
    {
        await using var db = CreateDbContext();

        var review = new Review
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            Rating = 5,
            Comment = "Great.",
            IsApproved = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Reviews.Add(review);

        await db.SaveChangesAsync();

        var saved =
            await db.Reviews.FirstAsync();

        Assert.True(saved.IsApproved);
    }

    [Fact]
    public void Rating_ShouldBeBetweenOneAndFive()
    {
        var validRatings = new[]
        {
            1, 2, 3, 4, 5
        };

        Assert.All(
            validRatings,
            rating =>
                Assert.InRange(rating, 1, 5));
    }

    [Fact]
    public async Task Review_ShouldBelongToProduct()
    {
        await using var db = CreateDbContext();

        var productId = Guid.NewGuid();

        var review = new Review
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ProductId = productId,
            OrderId = Guid.NewGuid(),
            Rating = 5,
            Comment = "Excellent.",
            IsApproved = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Reviews.Add(review);

        await db.SaveChangesAsync();

        var saved =
            await db.Reviews.FirstAsync();

        Assert.Equal(
            productId,
            saved.ProductId);
    }
}