using Microsoft.AspNetCore.Mvc; 
using Microsoft.EntityFrameworkCore; 
using ProductService.Data; 
using ProductService.DTOs; 
using ProductService.Models; 

namespace ProductService.Controllers; 
[ApiController] 
[Route("api/products")] 

public class ProductsController : ControllerBase 
{ 
    private readonly ProductDbContext _dbContext; 
    public ProductsController(ProductDbContext dbContext) 
    { 
        _dbContext = dbContext; 
    } 
    
    // CREATE PRODUCT 
    [HttpPost] 
    public async Task<IActionResult> Create( 
        CreateProductRequest request) 
        
    { 
        var categoryExists = await _dbContext.Categories 
            .AnyAsync(c => c.Id == request.CategoryId); 
        if (!categoryExists) 
        { 
            return BadRequest(new 
            { 
                message = "Category does not exist." 
            }); 
        } 
        
        var skuExists = await _dbContext.Products 
            .AnyAsync(p => p.SKU == request.SKU); 
        
        if (skuExists) 
        { 
            return Conflict(new 
            { 
                message = "A product with this SKU already exists." 
            }); 
        } 
        
        var slug = GenerateSlug(request.Name); 
        var slugExists = await _dbContext.Products 
            .AnyAsync(p => p.Slug == slug); 
        if (slugExists) 
        { 
            slug = $"{slug}-{Guid.NewGuid():N}"; 
        } 

        var product = new Product 
        { 
            Id = Guid.NewGuid(), 
            CategoryId = request.CategoryId, 
            Name = request.Name.Trim(), 
            Slug = slug, 
            Description = request.Description.Trim(), 
            Price = request.Price, 
            DiscountPrice = request.DiscountPrice, 
            Brand = request.Brand.Trim(), 
            SKU = request.SKU.Trim(), 
            IsActive = true, 
            CreatedAt = DateTime.UtcNow, 
            UpdatedAt = DateTime.UtcNow 
        }; 
            
        foreach (var imageUrl in request.ImageUrls) 
        { 
            if (string.IsNullOrWhiteSpace(imageUrl)) 
                continue; 
            product.Images.Add(new ProductImage 
            { 
                Id = Guid.NewGuid(), 
                ImageUrl = imageUrl.Trim(), 
                DisplayOrder = product.Images.Count, 
                IsPrimary = product.Images.Count == 0 
            }); 
            
        } 
        _dbContext.Products.Add(product); 
        await _dbContext.SaveChangesAsync(); 
        return CreatedAtAction( 
            nameof(GetById), 
            new { id = product.Id }, 
            ToResponse(product) 
        ); 
            
    } 
        
    // GET PRODUCT LIST 
    [HttpGet] 
    public async Task<IActionResult> GetProducts( 
        [FromQuery] string? search, 
        [FromQuery] Guid? categoryId, 
        [FromQuery] decimal? minPrice, 
        [FromQuery] decimal? maxPrice, 
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 12) 
    { 
        if (page < 1) 
        { 
            page = 1; 
        } 
        if (pageSize < 1) 
        { 
            pageSize = 12; 
        } 
        if (pageSize > 100) 
        { 
            pageSize = 100; 
        } 
        var query = _dbContext.Products 
            .AsNoTracking() 
            .Include(p => p.Category) 
            .Include(p => p.Images) 
            .Where(p => p.IsActive) 
            .AsQueryable(); 
        
        if (!string.IsNullOrWhiteSpace(search)) 
        { 
            search = search.Trim(); 
            query = query.Where(p => 
                p.Name.Contains(search) || 
                p.Brand.Contains(search) || 
                p.SKU.Contains(search) || 
                p.Description.Contains(search)); 
        } 
        
        if (categoryId.HasValue) 
        { 
            query = query.Where( p => p.CategoryId == categoryId.Value); 
        } 
        if (minPrice.HasValue) 
        { 
            query = query.Where( p => p.Price >= minPrice.Value); 
        } 
        if (maxPrice.HasValue) 
        { 
            query = query.Where( p => p.Price <= maxPrice.Value); 
        } 
        var totalItems = await query.CountAsync(); 
        var products = await query 
            .OrderByDescending(p => p.CreatedAt) 
            .Skip((page - 1) * pageSize) 
            .Take(pageSize) 
            .ToListAsync(); 
        
        var result = products 
            .Select(ToResponse) 
            .ToList(); 
            
        return Ok(new 
        { 
            page, 
            pageSize, 
            totalItems, 
            totalPages = (int)Math.Ceiling( totalItems / (double)pageSize), 
            items = result 
        }); 
    } 
        
    // GET SINGLE PRODUCT 
    [HttpGet("{id:guid}")] 
    public async Task<IActionResult> GetById(Guid id) 
    { 
        var product = await _dbContext.Products 
            .AsNoTracking() 
            .Include(p => p.Category) 
            .Include(p => p.Images) 
            .FirstOrDefaultAsync(p => p.Id == id); 
            
        if (product == null) 
        { 
            return NotFound(new 
            { 
                message = "Product not found." 
            }); 
        } 
        return Ok(ToResponse(product)); 
    } 
    private static ProductResponse ToResponse(Product product) 
    { 
        return new ProductResponse 
        { 
            Id = product.Id, 
            CategoryId = product.CategoryId, 
            CategoryName = product.Category?.Name ?? string.Empty, 
            Name = product.Name, 
            Slug = product.Slug, 
            Description = product.Description, 
            Price = product.Price, 
            DiscountPrice = product.DiscountPrice, 
            Brand = product.Brand, 
            SKU = product.SKU, 
            IsActive = product.IsActive, 
            ImageUrls = product.Images 
                .OrderBy(i => i.DisplayOrder) 
                .Select(i => i.ImageUrl) 
                .ToList() 
        }; 
    } 
    private static string GenerateSlug(string value) 
    { 
        return new string( 
            value 
                .ToLowerInvariant() 
                .Select(c => 
                    char.IsLetterOrDigit(c) 
                        ? c 
                        : '-') 
                .ToArray() 
        ).Trim('-'); 
    } 
}