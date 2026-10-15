using CartService.Data;
using CartService.DTOs;
using CartService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace CartService.Controllers;

[ApiController]
[Authorize]
[Route("api/cart")]
public class CartController : ControllerBase
{
    private readonly CartDbContext _dbContext;
    private readonly IHttpClientFactory _clients;

    public CartController(CartDbContext dbContext, IHttpClientFactory clients)
    {
        _dbContext = dbContext;
        _clients = clients;
    }

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetCart(Guid userId)
    {
        if (!OwnsCart(userId)) return Forbid();
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

            await _dbContext.SaveChangesAsync();
        }

        foreach (var item in cart.Items)
        {
            var price = await GetCurrentPrice(item.ProductId);
            if (price is null) return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Catalog pricing is unavailable.");
            item.UnitPrice = price.Value;
        }
        if (_dbContext.Entry(cart).State != EntityState.Detached) await _dbContext.SaveChangesAsync();

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
        if (!OwnsCart(userId)) return Forbid();
        if (request.Quantity < 1 || request.ProductId == Guid.Empty) return BadRequest();
        var currentPrice = await GetCurrentPrice(request.ProductId);
        if (currentPrice is null) return BadRequest(new { message = "Product is unavailable or pricing could not be verified." });
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
            existingItem.UnitPrice = currentPrice.Value;
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
                UnitPrice = currentPrice.Value,
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
    [HttpPatch("{userId:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> UpdateItem(
        Guid userId,
        Guid itemId,
        UpdateCartItemRequest request)
    {
        if (!OwnsCart(userId)) return Forbid();
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
        if (!OwnsCart(userId)) return Forbid();
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

    [HttpDelete("{userId:guid}/items")]
    public async Task<IActionResult> ClearCart(Guid userId)
    {
        if (!OwnsCart(userId)) return Forbid();
        var cart = await _dbContext.Carts.Include(x => x.Items).SingleOrDefaultAsync(x => x.UserId == userId);
        if (cart is null) return NoContent();
        _dbContext.CartItems.RemoveRange(cart.Items);
        cart.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    private bool OwnsCart(Guid requestedUserId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId) && currentUserId == requestedUserId;

    private async Task<decimal?> GetCurrentPrice(Guid productId)
    {
        try
        {
            var product = await _clients.CreateClient("products").GetFromJsonAsync<ProductPrice>($"api/products/{productId}", HttpContext.RequestAborted);
            if (product is null || !product.IsActive) return null;
            return product.DiscountPrice is > 0 ? product.DiscountPrice : product.Price;
        }
        catch (HttpRequestException) { return null; }
        catch (TaskCanceledException) { return null; }
    }

    private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    [HttpGet]
    public Task<IActionResult> GetCurrentCart() => CurrentUserId is Guid id ? GetCart(id) : Task.FromResult<IActionResult>(Unauthorized());

    [HttpPost("items")]
    public Task<IActionResult> AddCurrentItem(AddCartItemRequest request) => CurrentUserId is Guid id ? AddItem(id, request) : Task.FromResult<IActionResult>(Unauthorized());

    [HttpPatch("items/{itemId:guid}")]
    [HttpPut("items/{itemId:guid}")]
    public Task<IActionResult> UpdateCurrentItem(Guid itemId, UpdateCartItemRequest request) => CurrentUserId is Guid id ? UpdateItem(id, itemId, request) : Task.FromResult<IActionResult>(Unauthorized());

    [HttpDelete("items/{itemId:guid}")]
    public Task<IActionResult> RemoveCurrentItem(Guid itemId) => CurrentUserId is Guid id ? RemoveItem(id, itemId) : Task.FromResult<IActionResult>(Unauthorized());

    [HttpDelete("items")]
    public Task<IActionResult> ClearCurrentCart() => CurrentUserId is Guid id ? ClearCart(id) : Task.FromResult<IActionResult>(Unauthorized());

    [HttpPost("validate")]
    public async Task<IActionResult> ValidateCurrentCart()
    {
        if (CurrentUserId is not Guid userId) return Unauthorized();
        var cart = await _dbContext.Carts.AsNoTracking().Include(x => x.Items).SingleOrDefaultAsync(x => x.UserId == userId);
        if (cart is null || cart.Items.Count == 0) return Ok(new { valid = false, issues = new[] { new { productId = (Guid?)null, reason = "Cart is empty." } } });
        var client = _clients.CreateClient("products");
        var issues = new List<object>();
        foreach (var item in cart.Items)
        {
            try
            {
                var availability = await client.GetFromJsonAsync<AvailabilityInfo>($"api/products/{item.ProductId}/availability", HttpContext.RequestAborted);
                if (availability is null || availability.AvailableQuantity < item.Quantity)
                    issues.Add(new { productId = item.ProductId, reason = "Insufficient or unavailable stock." });
            }
            catch (HttpRequestException) { return Problem(statusCode: 503, title: "Product availability is unavailable."); }
        }
        return Ok(new { valid = issues.Count == 0, issues });
    }

    private sealed class ProductPrice { public decimal Price { get; set; } public decimal? DiscountPrice { get; set; } public bool IsActive { get; set; } }
    public sealed class AvailabilityInfo { public int AvailableQuantity { get; set; } }

}
