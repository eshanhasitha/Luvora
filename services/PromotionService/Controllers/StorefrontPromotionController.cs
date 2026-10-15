using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromotionService.Data;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace PromotionService.Controllers;

[ApiController]
[Route("api/promotions")]
public sealed class StorefrontPromotionController(PromotionDbContext db, IHttpClientFactory clients) : ControllerBase
{
    [HttpGet("active")]
    public async Task<IActionResult> Active(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return Ok(await db.Promotions.AsNoTracking().Where(x => x.IsActive && x.StartsAt <= now && x.EndsAt >= now && (x.UsageLimit == null || x.UsageCount < x.UsageLimit)).Select(x => new { x.Id, x.Name, x.Description, x.DiscountType, x.Value, x.MinimumOrderAmount, x.StartsAt, x.EndsAt }).ToListAsync(ct));
    }

    [HttpGet("{promotionId:guid}")]
    public async Task<IActionResult> Get(Guid promotionId)
    {
        var now = DateTime.UtcNow;
        var item = await db.Promotions.AsNoTracking().Where(x => x.Id == promotionId && x.IsActive && x.StartsAt <= now && x.EndsAt >= now).Select(x => new { x.Id, x.Name, x.Description, x.DiscountType, x.Value, x.MinimumOrderAmount, x.StartsAt, x.EndsAt }).SingleOrDefaultAsync();
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("coupons/validate")]
    [Authorize]
    public async Task<IActionResult> ValidateCoupon(CouponValidationRequest request)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var customerId)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Code)) return BadRequest(new { valid = false, message = "Coupon code is required." });
        var cartClient = clients.CreateClient("carts");
        var authorization = Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization)) cartClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorization);
        CartSummary? cart;
        try { cart = await cartClient.GetFromJsonAsync<CartSummary>($"api/cart/{customerId}", HttpContext.RequestAborted); }
        catch (HttpRequestException) { return Problem(statusCode: 503, title: "Cart service is unavailable."); }
        if (cart is null || cart.TotalItems < 1) return BadRequest(new { valid = false, message = "Cart is empty or unavailable." });
        var now = DateTime.UtcNow;
        var code = request.Code.Trim().ToUpperInvariant();
        var coupon = await db.Coupons.AsNoTracking().SingleOrDefaultAsync(x => x.Code == code && x.IsActive && x.StartsAt <= now && x.EndsAt >= now && (x.UsageLimit == null || x.UsageCount < x.UsageLimit));
        if (coupon is null) return BadRequest(new { valid = false, message = "Coupon is invalid or unavailable." });
        if (cart.TotalAmount < coupon.MinimumOrderAmount) return BadRequest(new { valid = false, message = "Order does not meet the coupon minimum." });
        var discount = coupon.DiscountType.Equals("Percent", StringComparison.OrdinalIgnoreCase) ? cart.TotalAmount * coupon.Value / 100m : coupon.Value;
        discount = Math.Min(cart.TotalAmount, Math.Max(0, discount));
        return Ok(new { valid = true, couponId = coupon.Id, discount, isCombinable = coupon.IsCombinable });
    }

    public sealed record CouponValidationRequest(string Code);
    public sealed class CartSummary { public decimal TotalAmount { get; set; } public int TotalItems { get; set; } }
}
