using DeliveryService.Data;
using DeliveryService.DTOs;
using DeliveryService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Net.Http.Json;

namespace DeliveryService.Controllers;

[ApiController]
[Authorize]
[Route("api/deliveries")]
public class DeliveryController : ControllerBase
{
    private readonly DeliveryDbContext _dbContext;
    private readonly IHttpClientFactory _clients;

    public DeliveryController(
        DeliveryDbContext dbContext,
        IHttpClientFactory clients)
    {
        _dbContext = dbContext;
        _clients = clients;
    }

    [HttpPost]
    public async Task<IActionResult> CreateDelivery(
        CreateDeliveryRequest request)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId)) return Unauthorized();
        if (request.UserId != currentUserId && !User.IsInRole("Admin")) return Forbid();

        var orders = _clients.CreateClient("orders");
        var authorization = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization)) orders.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorization);
        OrderSummary? order;
        try
        {
            var orderPath = request.UserId == currentUserId
                ? $"api/users/me/orders/{request.OrderId}"
                : $"api/admin/orders/{request.OrderId}";
            using var response = await orders.GetAsync(orderPath, HttpContext.RequestAborted);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return BadRequest(new { message = "The order does not belong to the specified customer." });
            if (!response.IsSuccessStatusCode)
                return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Order ownership could not be verified.");
            order = await response.Content.ReadFromJsonAsync<OrderSummary>(cancellationToken: HttpContext.RequestAborted);
        }
        catch (HttpRequestException)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Order ownership could not be verified.");
        }
        catch (TaskCanceledException) when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Order ownership could not be verified.");
        }

        if (order is null || order.UserId != request.UserId)
            return BadRequest(new { message = "The order does not belong to the specified customer." });
        if (order.Status is not ("CONFIRMED" or "PROCESSING"))
            return Conflict(new { message = "A delivery can only be created for a confirmed or processing order.", currentStatus = order.Status });
        var existingDelivery =
            await _dbContext.Deliveries
                .FirstOrDefaultAsync(
                    d => d.OrderId == request.OrderId);

        if (existingDelivery != null)
        {
            return Conflict(new
            {
                message =
                    "A delivery already exists for this order."
            });
        }

        var delivery = new Delivery
        {
            Id = Guid.NewGuid(),
            OrderId = request.OrderId,
            UserId = request.UserId,
            Address = request.Address,
            Status = "PENDING",
            TrackingNumber =
                $"LUV-TRK-{Random.Shared.Next(100000, 999999)}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Deliveries.Add(delivery);

        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetDelivery),
            new { deliveryId = delivery.Id },
            ToResponse(delivery)
        );
    }

    private sealed class OrderSummary
    {
        public Guid UserId { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    [HttpGet("{deliveryId:guid}")]
    public async Task<IActionResult> GetDelivery(
        Guid deliveryId)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId)) return Unauthorized();
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("Staff");
        var delivery =
            await _dbContext.Deliveries
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    d => d.Id == deliveryId && (d.UserId == currentUserId || isAdmin));

        if (delivery == null)
        {
            return NotFound(new
            {
                message = "Delivery not found."
            });
        }

        return Ok(ToResponse(delivery));
    }

    [HttpGet("order/{orderId:guid}")]
    [HttpGet("/api/users/me/orders/{orderId:guid}/shipment")]
    public async Task<IActionResult> GetByOrder(
        Guid orderId)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId)) return Unauthorized();
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("Staff");
        var delivery =
            await _dbContext.Deliveries
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    d => d.OrderId == orderId && (d.UserId == currentUserId || isAdmin));

        if (delivery == null)
        {
            return NotFound(new
            {
                message =
                    "Delivery not found for this order."
            });
        }

        return Ok(ToResponse(delivery));
    }

    [HttpGet("/api/users/me/orders/{orderId:guid}/tracking")]
    public async Task<IActionResult> GetCustomerTracking(Guid orderId)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var delivery = await _dbContext.Deliveries.AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == orderId && x.UserId == userId);
        if (delivery is null) return NotFound();
        var events = new List<object> { new { status = "PENDING", occurredAt = delivery.CreatedAt } };
        if (delivery.ShippedAt.HasValue) events.Add(new { status = "SHIPPED", occurredAt = delivery.ShippedAt.Value });
        if (delivery.DeliveredAt.HasValue) events.Add(new { status = "DELIVERED", occurredAt = delivery.DeliveredAt.Value });
        return Ok(new { delivery.OrderId, delivery.TrackingNumber, currentStatus = delivery.Status, events });
    }

    [HttpPut("{deliveryId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid deliveryId,
        [FromQuery] string status)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Staff")) return Forbid();
        var delivery =
            await _dbContext.Deliveries
                .FirstOrDefaultAsync(
                    d => d.Id == deliveryId);

        if (delivery == null)
        {
            return NotFound(new
            {
                message = "Delivery not found."
            });
        }

        var allowedStatuses = new[]
        {
            "PENDING",
            "PROCESSING",
            "SHIPPED",
            "DELIVERED"
        };

        if (!allowedStatuses.Contains(status))
        {
            return BadRequest(new
            {
                message = "Invalid delivery status."
            });
        }

        delivery.Status = status;
        delivery.UpdatedAt = DateTime.UtcNow;

        if (status == "SHIPPED" &&
            delivery.ShippedAt == null)
        {
            delivery.ShippedAt = DateTime.UtcNow;
        }

        if (status == "DELIVERED" &&
            delivery.DeliveredAt == null)
        {
            delivery.DeliveredAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();

        return Ok(ToResponse(delivery));
    }

    private static DeliveryResponse ToResponse(
        Delivery delivery)
    {
        return new DeliveryResponse
        {
            Id = delivery.Id,
            OrderId = delivery.OrderId,
            UserId = delivery.UserId,
            Address = delivery.Address,
            Status = delivery.Status,
            TrackingNumber = delivery.TrackingNumber,
            ShippedAt = delivery.ShippedAt,
            DeliveredAt = delivery.DeliveredAt,
            CreatedAt = delivery.CreatedAt
        };
    }
}
