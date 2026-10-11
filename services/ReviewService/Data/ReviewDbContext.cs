using Microsoft.EntityFrameworkCore;
using ReviewService.Models;

namespace ReviewService.Data;

public class ReviewDbContext : DbContext
{
    public ReviewDbContext(
        DbContextOptions<ReviewDbContext> options)
        : base(options)
    {
    }

    public DbSet<Review> Reviews => Set<Review>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Review>(entity =>
        {
            entity.ToTable("reviews");

            entity.HasKey(r => r.Id);

            entity.Property(r => r.UserId)
                .IsRequired();

            entity.Property(r => r.ProductId)
                .IsRequired();

            entity.Property(r => r.OrderId);

            entity.Property(r => r.Title).HasMaxLength(150).IsRequired();

            entity.Property(r => r.Rating)
                .IsRequired();

            entity.Property(r => r.Comment)
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(r => r.IsApproved)
                .IsRequired();

            entity.Property(r => r.CreatedAt)
                .IsRequired();

            entity.Property(r => r.UpdatedAt)
                .IsRequired();

            entity.HasIndex(r => new
            {
                r.UserId,
                r.ProductId,
                r.OrderId
            })
            .IsUnique();

            entity.HasIndex(r => new { r.UserId, r.ProductId })
                .IsUnique()
                .HasFilter("\"OrderId\" IS NULL");

            entity.HasIndex(r => r.ProductId);
        });
    }
}
