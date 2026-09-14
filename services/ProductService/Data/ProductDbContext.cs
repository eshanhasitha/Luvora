using Microsoft.EntityFrameworkCore; 
using ProductService.Models; 

namespace ProductService.Data; 

public class ProductDbContext : DbContext 
{ 
    public ProductDbContext( 
        DbContextOptions<ProductDbContext> options) 
        : base(options)
    {
        
    } 
    
    public DbSet<Product> Products => Set<Product>(); 
    public DbSet<Category> Categories => Set<Category>(); 
    public DbSet<ProductImage> ProductImages => Set<ProductImage>(); 
    protected override void OnModelCreating(ModelBuilder modelBuilder) 
    { 
        base.OnModelCreating(modelBuilder); 
        // Category 
        modelBuilder.Entity<Category>(entity => 
        { 
            entity.ToTable("categories"); 
            entity.HasKey(c => c.Id); 
            entity.Property(c => c.Name) .IsRequired() .HasMaxLength(150); 
            entity.Property(c => c.Slug) .IsRequired() .HasMaxLength(180); 
            entity.Property(c => c.Description) .HasMaxLength(1000); 
            entity.HasIndex(c => c.Slug) .IsUnique(); 
        }); // Product 
        modelBuilder.Entity<Product>(entity => { 
            entity.ToTable("products"); 
            entity.HasKey(p => p.Id); 
            entity.Property(p => p.Name) .IsRequired() .HasMaxLength(250); 
            entity.Property(p => p.Slug) .IsRequired() .HasMaxLength(280); 
            entity.Property(p => p.Description) .HasMaxLength(5000); 
            entity.Property(p => p.Brand) .HasMaxLength(150); 
            entity.Property(p => p.SKU) .IsRequired() .HasMaxLength(100); 
            entity.Property(p => p.Price) .HasPrecision(18, 2); 
            entity.Property(p => p.DiscountPrice) .HasPrecision(18, 2); 
            entity.HasIndex(p => p.Slug) .IsUnique(); 
            entity.HasIndex(p => p.SKU) .IsUnique(); 
            entity.HasIndex(p => p.CategoryId); 
            entity.HasOne(p => p.Category) 
                .WithMany(c => c.Products) 
                .HasForeignKey(p => p.CategoryId) 
                .OnDelete(DeleteBehavior.Restrict); 
        }); 
        
        // Product Images 
        modelBuilder.Entity<ProductImage>(entity => 
        { 
            entity.ToTable("product_images"); 
            entity.HasKey(i => i.Id); 
            entity.Property(i => i.ImageUrl) .IsRequired() .HasMaxLength(1000); 
            entity.HasIndex(i => i.ProductId); 
            entity.HasOne(i => i.Product) 
                .WithMany(p => p.Images) 
                .HasForeignKey(i => i.ProductId) 
                .OnDelete(DeleteBehavior.Cascade); 
        }); 
    } 
}