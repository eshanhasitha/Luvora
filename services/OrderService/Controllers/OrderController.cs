using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.DTOs;
using OrderService.Models;
using OrderService.Events;
using OrderService.Application.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Net.Http.Json;

namespace OrderService.Controllers;

[ApiController]
[Authorize]
[Route("api/orders")]
public class OrderController : ControllerBase
{
    private readonly OrderDbContext _dbContext;
    private readonly EventPublisher _eventPublisher;
    private readonly IHttpClientFactory _clients;

    public OrderController(
        OrderDbContext dbContext,
        EventPublisher eventPublisher,
        IHttpClientFactory clients)
    {
        _dbContext = dbContext;
        _eventPublisher = eventPublisher;
        _clients = clients;
    }

    [HttpPost]
    [HttpPost("/api/users/me/orders")]
    public async Task<IActionResult> CreateOrder(
        CreateOrderRequest request)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId)) return Unauthorized();
        if (request.Items == null ||
            request.Items.Count == 0)
        {
            return BadRequest(new
            {
                message = "Order must contain at least one item."
            });
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = currentUserId,
            OrderNumber =
                $"LUV-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}",
            Status = "CREATED",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var products = _clients.CreateClient("products");
        var authorization = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization)) products.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorization);

        foreach (var requestItem in request.Items)
        {
            if (requestItem.Quantity < 1) return BadRequest(new { message = "Item quantities must be positive." });
            ProductPrice? product;
            try { product = await products.GetFromJsonAsync<ProductPrice>($"api/products/{requestItem.ProductId}", HttpContext.RequestAborted); }
            catch (HttpRequestException) { return Problem(statusCode: 503, title: "Catalog service is unavailable."); }
            if (product is null || !product.IsActive) return BadRequest(new { message = "An order item is unavailable." });
            Availability? stock;
            try { stock = await products.GetFromJsonAsync<Availability>($"api/products/{requestItem.ProductId}/availability", HttpContext.RequestAborted); }
            catch (HttpRequestException) { return Problem(statusCode: 503, title: "Inventory availability is unavailable."); }
            if (stock is null || stock.AvailableQuantity < requestItem.Quantity) return Conflict(new { message = "Insufficient stock for an order item.", productId = requestItem.ProductId });
            var unitPrice = product.DiscountPrice is > 0 ? product.DiscountPrice.Value : product.Price;
            var totalPrice = unitPrice * requestItem.Quantity;

            var item = new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ProductId = requestItem.ProductId,
                Quantity = requestItem.Quantity,
                UnitPrice = unitPrice,
                TotalPrice = totalPrice,
                CreatedAt = DateTime.UtcNow
            };

            order.Items.Add(item);
        }

        order.TotalAmount =
            order.Items.Sum(item => item.TotalPrice);

        _dbContext.Orders.Add(order);

        await _dbContext.SaveChangesAsync();

        var orderCreatedEvent = new OrderCreatedEvent
        {
            OrderId = order.Id,
            UserId = order.UserId,
            TotalAmount = order.TotalAmount,
            CreatedAt = order.CreatedAt
        };

        await _eventPublisher.PublishAsync(
            "order.created",
            orderCreatedEvent,
            HttpContext.RequestAborted);

        return CreatedAtAction(
            nameof(GetOrder),
            new { orderId = order.Id },
            ToResponse(order)
        );
    }

    [HttpGet("{orderId:guid}")]
    [HttpGet("/api/users/me/orders/{orderId:guid}")]
    public async Task<IActionResult> GetOrder(
        Guid orderId)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId)) return Unauthorized();
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                o => o.Id == orderId && o.UserId == currentUserId);

        if (order == null)
        {
            return NotFound(new
            {
                message = "Order not found."
            });
        }

        return Ok(ToResponse(order));
    }

    [HttpGet("/api/users/me/orders/{orderId:guid}/items")]
    public async Task<IActionResult> GetMyOrderItems(Guid orderId)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var order = await _dbContext.Orders.AsNoTracking().Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == orderId && x.UserId == userId);
        return order is null ? NotFound() : Ok(order.Items.Select(x => new { x.Id, x.ProductId, x.Quantity, x.UnitPrice, x.TotalPrice }));
    }

    private static OrderResponse ToResponse(
        Order order)
    {
        return new OrderResponse
        {
            Id = order.Id,
            UserId = order.UserId,
            OrderNumber = order.OrderNumber,
            Status = order.Status,
            TotalAmount = order.TotalAmount,
            CreatedAt = order.CreatedAt,

            Items = order.Items
                .Select(item => new OrderItemResponse
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = item.TotalPrice
                })
                .ToList()
        };
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<IActionResult> GetUserOrders(
        Guid userId)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId)) return Unauthorized();
        if (currentUserId != userId) return Forbid();
        var orders = await _dbContext.Orders
            .Include(o => o.Items)
            .AsNoTracking()
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var response = orders
            .Select(ToResponse)
            .ToList();

        return Ok(response);
    }

    [HttpGet("/api/users/me/orders")]
    public async Task<IActionResult> GetMyOrders()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var orders = await _dbContext.Orders.Include(x => x.Items).AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).ToListAsync();
        return Ok(orders.Select(ToResponse));
    }

    private sealed class ProductPrice
    {
        public decimal Price { get; set; }
        public decimal? DiscountPrice { get; set; }
        public bool IsActive { get; set; }
    }
    public sealed class Availability { public int AvailableQuantity { get; set; } }

    [HttpPost("{orderId:guid}/cancel")]
    [HttpPost("/api/users/me/orders/{orderId:guid}/cancel")]
    public async Task<IActionResult> CancelOrder(
        Guid orderId)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId)) return Unauthorized();
        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(
                o => o.Id == orderId && o.UserId == currentUserId);

        if (order == null)
        {
            return NotFound(new
            {
                message = "Order not found."
            });
        }

        var cancellableStatuses = new[]
        {
            "CREATED",
            "PAYMENT_PENDING"
        };

        if (!cancellableStatuses.Contains(order.Status))
        {
            return Conflict(new
            {
                message =
                    "Order cannot be cancelled in its current status.",
                currentStatus = order.Status
            });
        }

        order.Status = "CANCELLED";
        order.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            message = "Order cancelled successfully.",
            orderId = order.Id,
            orderNumber = order.OrderNumber,
            status = order.Status
        });
    }
}
