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

    [HttpPost("{userId:guid}/items")]
    public async Task<IActionResult> AddItem(
        Guid userId,
        AddCartItemRequest request)
    {
        var cart = await _dbContext.Carts
            .Include(c => c.Items)
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
        }

        var existingItem = cart.Items
            .FirstOrDefault(i =>
                i.ProductId == request.ProductId);

        if (existingItem != null)
        {
            existingItem.Quantity += request.Quantity;
            existingItem.UnitPrice = request.UnitPrice;
            existingItem.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var item = new CartItem
            {
                Id = Guid.NewGuid(),
                CartId = cart.Id,
                ProductId = request.ProductId,
                Quantity = request.Quantity,
                UnitPrice = request.UnitPrice,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.CartItems.Add(item);
        }

        cart.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return Ok(ToResponse(cart));
    }

    [HttpPut("{userId:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> UpdateItem(
        Guid userId,
        Guid itemId,
        UpdateCartItemRequest request)
    {
        var cart = await _dbContext.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null)
        {
            return NotFound(new
            {
                message = "Cart not found."
            });
        }

        var item = cart.Items
            .FirstOrDefault(i => i.Id == itemId);

        if (item == null)
        {
            return NotFound(new
            {
                message = "Cart item not found."
            });
        }

        item.Quantity = request.Quantity;
        item.UpdatedAt = DateTime.UtcNow;

        cart.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return Ok(ToResponse(cart));
    }

    [HttpDelete("{userId:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> RemoveItem(
        Guid userId,
        Guid itemId)
    {
        var cart = await _dbContext.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null)
        {
            return NotFound(new
            {
                message = "Cart not found."
            });
        }

        var item = cart.Items
            .FirstOrDefault(i => i.Id == itemId);

        if (item == null)
        {
            return NotFound(new
            {
                message = "Cart item not found."
            });
        }

        _dbContext.CartItems.Remove(item);

        cart.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            message = "Cart item removed successfully."
        });
    }

}