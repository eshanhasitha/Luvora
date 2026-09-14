namespace ProductService.Models; 
public class Product 
{ 
    public Guid Id { get; set; } 
    public Guid CategoryId { get; set; } 
    public string Name { get; set; } = string.Empty; 
    public string Slug { get; set; } = string.Empty; 
    public string Description { get; set; } = string.Empty; 
    public decimal Price { get; set; } 
    public decimal? DiscountPrice { get; set; } 
    public string Brand { get; set; } = string.Empty; 
    public string SKU { get; set; } = string.Empty; 
    public bool IsActive { get; set; } = true; 
    public DateTime CreatedAt { get; set; } 
    public DateTime UpdatedAt { get; set; } 
    public Category? Category { get; set; } 
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>(); }