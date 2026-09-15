using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductService.Controllers;
using ProductService.Data;
using ProductService.DTOs;
using ProductService.Models;
using Xunit;

namespace ProductService.Tests;

public class CategoryControllerTests
{
    [Fact]
    public async Task CreateCategory_WithJsonNameObject_ReturnsCreatedCategory()
    {
        var options = new DbContextOptionsBuilder<ProductDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new ProductDbContext(options);
        var controller = new ProductsController(dbContext);

        var result = await controller.CreateCategory(new CreateCategoryRequest
        {
            Name = "Clothing"
        });

        var createdResult = Assert.IsType<CreatedResult>(result);
        var category = Assert.IsType<Category>(createdResult.Value);

        Assert.Equal("Clothing", category.Name);
        Assert.Equal("clothing", category.Slug);
    }

    [Fact]
    public async Task Update_WhenProductNameDoesNotChange_KeepsExistingSlug()
    {
        var options = new DbContextOptionsBuilder<ProductDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new ProductDbContext(options);
        var controller = new ProductsController(dbContext);

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Electronics",
            Slug = "electronics",
            Description = "Electronics",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Name = "Blue Shirt",
            Slug = "blue-shirt",
            Description = "Old description",
            Price = 100m,
            Brand = "Brand",
            SKU = "SKU-001",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await dbContext.Categories.AddAsync(category);
        await dbContext.Products.AddAsync(product);
        await dbContext.SaveChangesAsync();

        var result = await controller.Update(product.Id, new UpdateProductRequest
        {
            Name = "Blue Shirt",
            CategoryId = category.Id,
            Description = "Updated description",
            Price = 150m,
            Brand = "Brand",
            SKU = "SKU-001",
            IsActive = true,
            ImageUrls = new List<string> { "https://example.com/shirt.jpg" }
        });

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ProductService.DTOs.ProductResponse>(okResult.Value);

        Assert.Equal("blue-shirt", response.Slug);
    }

    [Fact]
    public async Task Update_WhenSlugAlreadyExists_UsesNumberedSuffix()
    {
        var options = new DbContextOptionsBuilder<ProductDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new ProductDbContext(options);
        var controller = new ProductsController(dbContext);

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Electronics",
            Slug = "electronics",
            Description = "Electronics",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var firstProduct = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Name = "Blue Shirt",
            Slug = "blue-shirt",
            Description = "Old description",
            Price = 100m,
            Brand = "Brand",
            SKU = "SKU-001",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var secondProduct = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Name = "Red Shirt",
            Slug = "red-shirt",
            Description = "Another description",
            Price = 120m,
            Brand = "Brand",
            SKU = "SKU-002",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await dbContext.Categories.AddAsync(category);
        await dbContext.Products.AddRangeAsync(firstProduct, secondProduct);
        await dbContext.SaveChangesAsync();

        var result = await controller.Update(firstProduct.Id, new UpdateProductRequest
        {
            Name = "Red Shirt",
            CategoryId = category.Id,
            Description = "Updated description",
            Price = 150m,
            Brand = "Brand",
            SKU = "SKU-003",
            IsActive = true,
            ImageUrls = new List<string> { "https://example.com/shirt.jpg" }
        });

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ProductService.DTOs.ProductResponse>(okResult.Value);

        Assert.StartsWith("red-shirt-", response.Slug);
        Assert.DoesNotContain(Guid.Empty.ToString(), response.Slug, StringComparison.OrdinalIgnoreCase);
    }
}
