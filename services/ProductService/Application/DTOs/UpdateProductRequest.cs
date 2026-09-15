using System.ComponentModel.DataAnnotations; 

namespace ProductService.DTOs; 

public class UpdateProductRequest 
{ 
    [Required] 
    [MaxLength(250)] 
    public string Name { get; set; } = string.Empty; 
    [Required] 
    public Guid CategoryId { get; set; } 
    [Required] 
    [MaxLength(5000)] 
    public string Description { get; set; } = string.Empty; 
    [Range(0.01, 999999999)] 
    public decimal Price { get; set; } 
    [Range(0.01, 999999999)] 
    public decimal? DiscountPrice { get; set; } 
    [MaxLength(150)] 
    public string Brand { get; set; } = string.Empty; 
    [Required] 
    [MaxLength(100)] 
    public string SKU { get; set; } = string.Empty; 
    public bool IsActive { get; set; } 
    public List<string> ImageUrls { get; set; } = new(); }