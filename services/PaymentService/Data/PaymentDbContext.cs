using Microsoft.EntityFrameworkCore;
using PaymentService.Models;

namespace PaymentService.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(
        DbContextOptions<PaymentDbContext> options)
        : base(options)
    {
    }

    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("payments");

            entity.HasKey(p => p.Id);

            entity.Property(p => p.OrderId)
                .IsRequired();

            entity.Property(p => p.UserId)
                .IsRequired();

            entity.Property(p => p.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(p => p.PaymentMethod)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(p => p.Status)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(p => p.TransactionId)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(p => p.CreatedAt)
                .IsRequired();

            entity.Property(p => p.UpdatedAt)
                .IsRequired();

            entity.HasIndex(p => p.OrderId)
                .IsUnique();

            entity.HasIndex(p => p.TransactionId)
                .IsUnique();
        });
    }
}