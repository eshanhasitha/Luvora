using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.DTOs;
using OrderService.Models;
using OrderService.Events;
using OrderService.Application.Services;

namespace OrderService.Controllers;

[ApiController]
[Route("api/orders")]
public class OrderController : ControllerBase
{
    private readonly OrderDbContext _dbContext;
    private readonly EventPublisher _eventPublisher;

    public OrderController(
        OrderDbContext dbContext,
        EventPublisher eventPublisher)
    {
        _dbContext = dbContext;
        _eventPublisher = eventPublisher;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder(
        CreateOrderRequest request)
    {
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
            UserId = request.UserId,
            OrderNumber =
                $"LUV-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}",
            Status = "CREATED",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var requestItem in request.Items)
        {
            var totalPrice =
                requestItem.UnitPrice *
                requestItem.Quantity;

            var item = new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ProductId = requestItem.ProductId,
                Quantity = requestItem.Quantity,
                UnitPrice = requestItem.UnitPrice,
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
    public async Task<IActionResult> GetOrder(
        Guid orderId)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                o => o.Id == orderId);

        if (order == null)
        {
            return NotFound(new
            {
                message = "Order not found."
            });
        }

        return Ok(ToResponse(order));
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

    [HttpPost("{orderId:guid}/cancel")]
    public async Task<IActionResult> CancelOrder(
        Guid orderId)
    {
        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(
                o => o.Id == orderId);

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