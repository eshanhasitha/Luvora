using Microsoft.EntityFrameworkCore;
using NotificationService.Models;

namespace NotificationService.Data;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(
        DbContextOptions<NotificationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Notification> Notifications =>
        Set<Notification>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");

            entity.HasKey(n => n.Id);

            entity.Property(n => n.UserId)
                .IsRequired();

            entity.Property(n => n.Type)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(n => n.Title)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(n => n.Message)
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(n => n.IsRead)
                .IsRequired();

            entity.Property(n => n.CreatedAt)
                .IsRequired();

            entity.HasIndex(n => n.UserId);

            entity.HasIndex(n => new
            {
                n.UserId,
                n.IsRead
            });
        });
    }
}