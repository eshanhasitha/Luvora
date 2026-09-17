using InventoryService.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Data;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(
        DbContextOptions<InventoryDbContext> options)
        : base(options)
    {
    }

    public DbSet<Inventory> Inventories => Set<Inventory>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Inventory>(entity =>
        {
            entity.ToTable("inventories");

            entity.HasKey(i => i.Id);

            entity.Property(i => i.ProductId)
                .IsRequired();

            entity.Property(i => i.AvailableQuantity)
                .IsRequired();

            entity.Property(i => i.ReservedQuantity)
                .IsRequired();

            entity.Property(i => i.ReorderLevel)
                .IsRequired();

            entity.Property(i => i.IsActive)
                .IsRequired();

            entity.Property(i => i.CreatedAt)
                .IsRequired();

            entity.Property(i => i.UpdatedAt)
                .IsRequired();

            entity.HasIndex(i => i.ProductId)
                .IsUnique();
        });
    }
}