using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaymentService.Data;
using PaymentService.DTOs;
using PaymentService.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Net.Http.Json;

namespace PaymentService.Controllers;

[ApiController]
[Authorize]
[Route("api/payments")]
public class PaymentController : ControllerBase
{
    private readonly PaymentDbContext _dbContext;

    private readonly IHttpClientFactory _clients;

    public PaymentController(PaymentDbContext dbContext, IHttpClientFactory clients)
    {
        _dbContext = dbContext;
        _clients = clients;
    }

    [HttpPost]
    public async Task<IActionResult> CreatePayment(
        CreatePaymentRequest request)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId)) return Unauthorized();
        var orders = _clients.CreateClient("orders");
        var authorization = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization)) orders.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorization);
        OrderSummary? order;
        try { order = await orders.GetFromJsonAsync<OrderSummary>($"api/users/me/orders/{request.OrderId}", HttpContext.RequestAborted); }
        catch (HttpRequestException) { return Problem(statusCode: 503, title: "Order service is unavailable."); }
        if (order is null || order.UserId != currentUserId) return NotFound(new { message = "Order not found." });
        if (order.Status is "CANCELLED" or "COMPLETED") return Conflict(new { message = "Order is not eligible for payment." });
        var existingPayment =
            await _dbContext.Payments
                .FirstOrDefaultAsync(
                    p => p.OrderId == request.OrderId);

        if (existingPayment != null)
        {
            return Conflict(new
            {
                message =
                    "A payment already exists for this order."
            });
        }

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = request.OrderId,
            UserId = currentUserId,
            Amount = order.TotalAmount,
            PaymentMethod = request.PaymentMethod,
            Status = "PENDING",
            TransactionId =
                $"TXN-{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Payments.Add(payment);

        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetPayment),
            new { paymentId = payment.Id },
            ToResponse(payment)
        );
    }

    public sealed class OrderSummary { public Guid UserId { get; set; } public decimal TotalAmount { get; set; } public string Status { get; set; } = string.Empty; }

    [HttpGet("{paymentId:guid}")]
    public async Task<IActionResult> GetPayment(
        Guid paymentId)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId)) return Unauthorized();
        var payment =
            await _dbContext.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.Id == paymentId && p.UserId == currentUserId);

        if (payment == null)
        {
            return NotFound(new
            {
                message = "Payment not found."
            });
        }

        return Ok(ToResponse(payment));
    }

    private static PaymentResponse ToResponse(
        Payment payment)
    {
        return new PaymentResponse
        {
            Id = payment.Id,
            OrderId = payment.OrderId,
            UserId = payment.UserId,
            Amount = payment.Amount,
            PaymentMethod = payment.PaymentMethod,
            Status = payment.Status,
            TransactionId = payment.TransactionId,
            CreatedAt = payment.CreatedAt
        };
    }

    [HttpGet("order/{orderId:guid}")]
    [HttpGet("orders/{orderId:guid}/transactions")]
    public async Task<IActionResult> GetPaymentByOrder(
        Guid orderId)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId)) return Unauthorized();
        var payment =
            await _dbContext.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.OrderId == orderId && p.UserId == currentUserId);

        if (payment == null)
        {
            return NotFound(new
            {
                message =
                    "Payment not found for this order."
            });
        }

        return Ok(ToResponse(payment));
    }
}
