using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductService.Data;
using ProductService.DTOs;
using ProductService.Models;

namespace ProductService.Controllers;

[ApiController]
[Route("api/admin/products")]
public sealed class AdminProductsController(ProductDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] Guid? categoryId,
        [FromQuery] string? brandId, [FromQuery] bool? status, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Products.AsNoTracking().Include(p => p.Category).Include(p => p.Images).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(p => p.Name.Contains(search) || p.SKU.Contains(search) || p.Brand.Contains(search));
        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(brandId)) query = query.Where(p => p.Brand == brandId);
        if (status.HasValue) query = query.Where(p => p.IsActive == status);
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(p => p.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new { page, pageSize, totalItems = total, products = items });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateProductRequest request)
    {
        if (!await db.Categories.AnyAsync(x => x.Id == request.CategoryId)) return BadRequest(new { message = "Category does not exist." });
        if (await db.Products.AnyAsync(x => x.SKU == request.SKU.Trim())) return Conflict(new { message = "A product with this SKU already exists." });
        var slugBase = Slug(request.Name);
        if (slugBase.Length == 0) return BadRequest(new { message = "Product name is invalid." });
        var slug = slugBase; var suffix = 2;
        while (await db.Products.AnyAsync(x => x.Slug == slug)) slug = $"{slugBase}-{suffix++}";
        var product = new Product { Id = Guid.NewGuid(), CategoryId = request.CategoryId, Name = request.Name.Trim(), Slug = slug,
            Description = request.Description.Trim(), Price = request.Price, DiscountPrice = request.DiscountPrice,
            Brand = request.Brand.Trim(), SKU = request.SKU.Trim(), IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        foreach (var url in request.ImageUrls.Where(x => !string.IsNullOrWhiteSpace(x)))
            product.Images.Add(new ProductImage { Id = Guid.NewGuid(), ImageUrl = url.Trim(), DisplayOrder = product.Images.Count, IsPrimary = product.Images.Count == 0 });
        db.Products.Add(product); await db.SaveChangesAsync();
        return Created($"/api/admin/products/{product.Id}", new { product.Id, product.Name, product.Slug, product.CategoryId, product.Price, product.SKU, product.IsActive });
    }

    [HttpPatch("{productId:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid productId, StatusRequest request)
    {
        var product = await db.Products.SingleOrDefaultAsync(x => x.Id == productId);
        if (product is null) return NotFound();
        var status = request.Status.Trim().ToLowerInvariant();
        if (status is not ("published" or "unpublished" or "archived"))
            return BadRequest(new { message = "Status must be published, unpublished, or archived." });
        product.IsActive = status == "published";
        product.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync();
        return Ok(new { product.Id, product.IsActive, product.UpdatedAt });
    }

    private static string Slug(string value) => new string(value.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
    public sealed record StatusRequest(string Status);
}
