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
}
