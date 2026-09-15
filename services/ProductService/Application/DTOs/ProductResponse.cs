namespace ProductService.DTOs; 
public class ProductResponse 
{ 
    public Guid Id { get; set; } 
    public Guid CategoryId { get; set; } 
    public string CategoryName { get; set; } = string.Empty; 
    public string Name { get; set; } = string.Empty; 
    public string Slug { get; set; } = string.Empty; 
    public string Description { get; set; } = string.Empty; 
    public decimal Price { get; set; } 
    public decimal? DiscountPrice { get; set; } 
    public string Brand { get; set; } = string.Empty; 
    public string SKU { get; set; } = string.Empty; 
    public bool IsActive { get; set; } 
    public List<string> ImageUrls { get; set; } = new(); 
}