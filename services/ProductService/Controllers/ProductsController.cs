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
        
        var baseSlug = GenerateSlug(request.Name); 
        if (string.IsNullOrWhiteSpace(baseSlug)) 
        { 
            return BadRequest(new { message = "Product name is invalid." }); 
        }

        var slug = baseSlug; 
        var slugSuffix = 2; 
        while (await _dbContext.Products.AnyAsync(p => p.Slug == slug)) 
        { 
            slug = $"{baseSlug}-{slugSuffix}"; 
            slugSuffix++; 
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

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { message = "Category name is required." });
        }

        var baseSlug = GenerateSlug(name);
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            return BadRequest(new { message = "Category name is invalid." });
        }

        var slug = baseSlug;
        var slugSuffix = 2;
        while (await _dbContext.Categories.AnyAsync(c => c.Slug == slug))
        {
            slug = $"{baseSlug}-{slugSuffix}";
            slugSuffix++;
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug,
            Description = $"{name} category",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync();

        return Created($"/api/products/categories/{category.Id}", category);
    }


    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateProductRequest request)
    {
        var product = await _dbContext.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return NotFound(new { message = "Product not found." });
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var categoryExists = await _dbContext.Categories
            .AnyAsync(c => c.Id == request.CategoryId);
        if (!categoryExists)
        {
            return BadRequest(new { message = "Category does not exist." });
        }

        var skuExists = await _dbContext.Products
            .AnyAsync(p => p.SKU == request.SKU && p.Id != id);

        if (skuExists)
        {
            return Conflict(new { message = "Another product already uses this SKU." });
        }

        var requestedName = request.Name.Trim();
        var generatedSlug = GenerateSlug(requestedName);
        if (string.IsNullOrWhiteSpace(generatedSlug))
        {
            return BadRequest(new { message = "Product name is invalid." });
        }

        if (string.Equals(product.Name.Trim(), requestedName, StringComparison.OrdinalIgnoreCase))
        {
            generatedSlug = product.Slug;
        }
        else
        {
            var baseSlug = generatedSlug;
            var candidateSlug = baseSlug;
            var suffix = 2;

            while (await _dbContext.Products.AnyAsync(p => p.Slug == candidateSlug && p.Id != id))
            {
                candidateSlug = $"{baseSlug}-{suffix}";
                suffix++;
            }

            generatedSlug = candidateSlug;
        }

        product.Name = requestedName;
        product.CategoryId = request.CategoryId;
        product.Description = request.Description.Trim();
        product.Price = request.Price;
        product.DiscountPrice = request.DiscountPrice;
        product.Brand = request.Brand.Trim();
        product.SKU = request.SKU.Trim();
        product.Slug = generatedSlug;
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        var existingImages = await _dbContext.ProductImages
            .Where(i => i.ProductId == id)
            .ToListAsync();

        if (existingImages.Count > 0)
        {
            _dbContext.ProductImages.RemoveRange(existingImages);
        }

        var newImageOrder = 0;
        foreach (var imageUrl in request.ImageUrls)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                continue;

            _dbContext.ProductImages.Add(new ProductImage
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                ImageUrl = imageUrl.Trim(),
                DisplayOrder = newImageOrder,
                IsPrimary = newImageOrder == 0
            });

            newImageOrder++;
        }

        await _dbContext.SaveChangesAsync();

        await _dbContext.Entry(product)
            .Reference(p => p.Category)
            .LoadAsync();

        return Ok(ToResponse(product));
    }

    [HttpDelete("{id:guid}")] 
    public async Task<IActionResult> Delete(Guid id) 
    { 
        var product = await _dbContext.Products 
            .FirstOrDefaultAsync(p => p.Id == id); 
        if (product == null) 
        { 
            return NotFound(new { message = "Product not found." }); 
        } 
        product.IsActive = false; 
        product.UpdatedAt = DateTime.UtcNow; 
        await _dbContext.SaveChangesAsync(); 
        return Ok(new { message = "Product deactivated successfully." }); 
    }

}