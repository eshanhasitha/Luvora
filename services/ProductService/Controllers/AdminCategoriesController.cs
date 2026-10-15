using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductService.Data;
using ProductService.Models;

namespace ProductService.Controllers;

[ApiController]
[Route("api/admin/categories")]
public sealed class AdminCategoriesController(ProductDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] bool? status)
    {
        var query = db.Categories.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search));
        if (status.HasValue) query = query.Where(x => x.IsActive == status);
        return Ok(await query.OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Slug, x.Description, x.IsActive }).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Create(CategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "Category name is required." });
        var name = request.Name.Trim();
        var slugBase = Slug(name); var slug = slugBase; var n = 2;
        if (slugBase.Length == 0) return BadRequest(new { message = "Category name is invalid." });
        while (await db.Categories.AnyAsync(x => x.Slug == slug)) slug = $"{slugBase}-{n++}";
        var category = new Category { Id = Guid.NewGuid(), Name = name, Slug = slug, Description = request.Description?.Trim() ?? string.Empty, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Categories.Add(category); await db.SaveChangesAsync();
        return Created($"/api/admin/categories/{category.Id}", category);
    }

    [HttpGet("{categoryId:guid}")]
    public async Task<IActionResult> Get(Guid categoryId)
    {
        var category = await db.Categories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == categoryId);
        return category is null ? NotFound() : Ok(category);
    }

    [HttpPut("{categoryId:guid}")]
    public async Task<IActionResult> Update(Guid categoryId, CategoryRequest request)
    {
        var category = await db.Categories.SingleOrDefaultAsync(x => x.Id == categoryId);
        if (category is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "Category name is required." });
        category.Name = request.Name.Trim();
        var slugBase = Slug(category.Name);
        if (slugBase.Length == 0) return BadRequest(new { message = "Category name is invalid." });
        var slug = slugBase; var suffix = 2;
        while (await db.Categories.AnyAsync(x => x.Slug == slug && x.Id != categoryId)) slug = $"{slugBase}-{suffix++}";
        category.Slug = slug; category.Description = request.Description?.Trim() ?? string.Empty; category.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(); return Ok(category);
    }

    [HttpPatch("{categoryId:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid categoryId, CategoryStatusRequest request)
    {
        var category = await db.Categories.SingleOrDefaultAsync(x => x.Id == categoryId);
        if (category is null) return NotFound();
        category.IsActive = request.Active; category.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync();
        return Ok(new { category.Id, category.IsActive, category.UpdatedAt });
    }

    [HttpDelete("{categoryId:guid}")]
    public async Task<IActionResult> Delete(Guid categoryId)
    {
        var category = await db.Categories.SingleOrDefaultAsync(x => x.Id == categoryId);
        if (category is null) return NotFound();
        if (await db.Products.AnyAsync(x => x.CategoryId == categoryId)) return Conflict(new { message = "Category is still assigned to products." });
        db.Categories.Remove(category); await db.SaveChangesAsync(); return NoContent();
    }

    private static string Slug(string value) => new string(value.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
    public sealed record CategoryRequest(string Name, string? Description);
    public sealed record CategoryStatusRequest(bool Active);
}
