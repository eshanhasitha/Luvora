using DeliveryService.Data;
using DeliveryService.DTOs;
using DeliveryService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliveryService.Controllers;

[ApiController]
[Route("api/deliveries")]
public class DeliveryController : ControllerBase
{
    private readonly DeliveryDbContext _dbContext;

    public DeliveryController(
        DeliveryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost]
    public async Task<IActionResult> CreateDelivery(
        CreateDeliveryRequest request)
    {
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

    [HttpGet("{deliveryId:guid}")]
    public async Task<IActionResult> GetDelivery(
        Guid deliveryId)
    {
        var delivery =
            await _dbContext.Deliveries
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    d => d.Id == deliveryId);

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
    public async Task<IActionResult> GetByOrder(
        Guid orderId)
    {
        var delivery =
            await _dbContext.Deliveries
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    d => d.OrderId == orderId);

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

    [HttpPut("{deliveryId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid deliveryId,
        [FromQuery] string status)
    {
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