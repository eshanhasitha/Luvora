using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PaymentService.Data;
using PaymentService.DTOs;
using PaymentService.Models;

namespace PaymentService.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentController : ControllerBase
{
    private readonly PaymentDbContext _dbContext;

    public PaymentController(
        PaymentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost]
    public async Task<IActionResult> CreatePayment(
        CreatePaymentRequest request)
    {
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
            UserId = request.UserId,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            Status = "PENDING",
            TransactionId =
                $"TXN-{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Payments.Add(payment);

        await _dbContext.SaveChangesAsync();

        // Mock payment processing.
        payment.Status = "COMPLETED";
        payment.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetPayment),
            new { paymentId = payment.Id },
            ToResponse(payment)
        );
    }

    [HttpGet("{paymentId:guid}")]
    public async Task<IActionResult> GetPayment(
        Guid paymentId)
    {
        var payment =
            await _dbContext.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.Id == paymentId);

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
    public async Task<IActionResult> GetPaymentByOrder(
        Guid orderId)
    {
        var payment =
            await _dbContext.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.OrderId == orderId);

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