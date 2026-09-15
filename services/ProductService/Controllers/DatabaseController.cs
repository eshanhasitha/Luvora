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
    
    [HttpPost] 
    public async Task<IActionResult> Create
    ( CreateProductRequest request) 
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
            { return Conflict(new 
            { 
                message = "A product with this SKU already exists." 
            }); 
        } 
        var slug = GenerateSlug(request.Name); 
        var slugExists = await _dbContext.Products 
            .AnyAsync(p => p.Slug == slug); 
        if (slugExists) 
        { 
            slug = $"{slug}-{Guid.NewGuid():N}"[..Math.Min( 
                slug.Length + 33, 
                280 
                )]; 
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
        var response = new ProductResponse 
        { 
            Id = product.Id, 
            CategoryId = product.CategoryId, 
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
        return CreatedAtAction( 
            nameof(GetById), 
            new { id = product.Id }, 
            response 
            ); 
        } 
    [HttpGet("{id:guid}")] 
    public async Task<IActionResult> GetById(Guid id) 
    { 
        var product = await _dbContext.Products 
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
            ImageUrls = product.Images .OrderBy(i => i.DisplayOrder) .Select(i => i.ImageUrl) .ToList() 
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

    [HttpPost("categories")] 
    public async Task<IActionResult> CreateCategory(
        [FromBody] CreateCategoryRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var normalizedName = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return BadRequest(new { message = "Category name is required." });
        }

        var slug = GenerateSlug(normalizedName);
        if (string.IsNullOrWhiteSpace(slug))
        {
            return BadRequest(new { message = "Category name is invalid." });
        }

        var slugExists = await _dbContext.Categories
            .AnyAsync(c => c.Slug == slug);

        if (slugExists)
        {
            slug = $"{slug}-{Guid.NewGuid():N}"[..Math.Min(slug.Length + 33, 180)];
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            Slug = slug,
            Description = $"{normalizedName} category",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync();
        return Created(
            $"/api/products/categories/{category.Id}",
            category
        );
    } 
}