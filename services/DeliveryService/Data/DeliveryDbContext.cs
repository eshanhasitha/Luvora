using DeliveryService.Models;
using Microsoft.EntityFrameworkCore;

namespace DeliveryService.Data;

public class DeliveryDbContext : DbContext
{
    public DeliveryDbContext(
        DbContextOptions<DeliveryDbContext> options)
        : base(options)
    {
    }

    public DbSet<Delivery> Deliveries => Set<Delivery>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Delivery>(entity =>
        {
            entity.ToTable("deliveries");

            entity.HasKey(d => d.Id);

            entity.Property(d => d.OrderId)
                .IsRequired();

            entity.Property(d => d.UserId)
                .IsRequired();

            entity.Property(d => d.Address)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(d => d.Status)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(d => d.TrackingNumber)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(d => d.CreatedAt)
                .IsRequired();

            entity.Property(d => d.UpdatedAt)
                .IsRequired();

            entity.HasIndex(d => d.OrderId)
                .IsUnique();

            entity.HasIndex(d => d.TrackingNumber)
                .IsUnique();
        });
    }
}