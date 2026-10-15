using Microsoft.EntityFrameworkCore;
using PromotionService.Models;

namespace PromotionService.Data;

public sealed class PromotionDbContext(DbContextOptions<PromotionDbContext> options) : DbContext(options)
{
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<Banner> Banners => Set<Banner>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Promotion>().HasIndex(x => x.Name);
        modelBuilder.Entity<Coupon>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Campaign>().HasIndex(x => x.Name);
        modelBuilder.Entity<Banner>().HasIndex(x => x.DisplayOrder);
    }
}
