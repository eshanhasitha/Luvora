using CartService.Data;
using CartService.DTOs;
using CartService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CartService.Controllers;

[ApiController]
[Route("api/cart")]
public class CartController : ControllerBase
{
    private readonly CartDbContext _dbContext;

    public CartController(CartDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetCart(Guid userId)
    {
        var cart = await _dbContext.Carts
            .Include(c => c.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null)
        {
            cart = new Cart
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Carts.Add(cart);

            await _dbContext.SaveChangesAsync();
        }

        return Ok(ToResponse(cart));
    }

    private static CartResponse ToResponse(Cart cart)
    {
        var items = cart.Items
            .Select(item => new CartItemResponse
            {
                Id = item.Id,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.UnitPrice * item.Quantity
            })
            .ToList();

        return new CartResponse
        {
            Id = cart.Id,
            UserId = cart.UserId,
            Items = items,
            TotalItems = items.Sum(i => i.Quantity),
            TotalAmount = items.Sum(i => i.TotalPrice)
        };
    }
}