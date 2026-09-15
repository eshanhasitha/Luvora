using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ProductService.DTOs;
using Xunit;

namespace ProductService.Tests;

public class ProductValidationTests
{
    [Fact]
    public void Product_Name_Is_Required()
    {
        var request = new CreateProductRequest
        {
            Name = string.Empty,
            CategoryId = Guid.NewGuid(),
            Description = "Test product",
            Price = 1000,
            Brand = "Luvora",
            SKU = "TEST-001"
        };

        var validationResults = Validate(request);

        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(CreateProductRequest.Name)));
    }

    [Fact]
    public void Product_Price_Must_Be_Greater_Than_Zero()
    {
        var request = new CreateProductRequest
        {
            Name = "Test Product",
            CategoryId = Guid.NewGuid(),
            Description = "Test product",
            Price = 0,
            Brand = "Luvora",
            SKU = "TEST-001"
        };

        var validationResults = Validate(request);

        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(CreateProductRequest.Price)));
    }

    [Fact]
    public void Product_SKU_Is_Required()
    {
        var request = new CreateProductRequest
        {
            Name = "Test Product",
            CategoryId = Guid.NewGuid(),
            Description = "Test product",
            Price = 1000,
            Brand = "Luvora",
            SKU = string.Empty
        };

        var validationResults = Validate(request);

        Assert.Contains(validationResults, result => result.MemberNames.Contains(nameof(CreateProductRequest.SKU)));
    }

    [Fact]
    public void Valid_Product_Should_Pass_Validation()
    {
        var request = new CreateProductRequest
        {
            Name = "Premium T-Shirt",
            CategoryId = Guid.NewGuid(),
            Description = "Premium cotton T-shirt",
            Price = 4500,
            DiscountPrice = 3990,
            Brand = "Luvora",
            SKU = "LUV-TSH-001"
        };

        var validationResults = Validate(request);

        Assert.Empty(validationResults);
    }

    private static List<ValidationResult> Validate(object model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }
}