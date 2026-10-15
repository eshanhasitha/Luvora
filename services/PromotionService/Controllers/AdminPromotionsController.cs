using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromotionService.Data;
using PromotionService.Models;

namespace PromotionService.Controllers;

[ApiController]
[Route("api/admin/promotions")]
public sealed class AdminPromotionsController(PromotionDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool? status, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Promotions.AsNoTracking().AsQueryable();
        if (status.HasValue) query = query.Where(x => x.IsActive == status);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search));
        var count = await query.CountAsync();
        return Ok(new { page, pageSize, totalItems = count, promotions = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync() });
    }

    [HttpPost]
    public async Task<IActionResult> Create(PromotionRequest request)
    {
        var error = ValidateDiscount(request.DiscountType, request.Value, request.MinimumOrderAmount, request.StartsAt, request.EndsAt, request.UsageLimit);
        if (error is not null) return BadRequest(new { message = error });
        var item = new Promotion { Id = Guid.NewGuid(), Name = request.Name.Trim(), Description = request.Description?.Trim() ?? string.Empty,
            DiscountType = request.DiscountType, Value = request.Value, MinimumOrderAmount = request.MinimumOrderAmount,
            EligibleProductIdsJson = System.Text.Json.JsonSerializer.Serialize(request.EligibleProductIds ?? []), StartsAt = request.StartsAt,
            EndsAt = request.EndsAt, UsageLimit = request.UsageLimit, IsCombinable = request.IsCombinable,
            IsActive = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        if (string.IsNullOrWhiteSpace(item.Name)) return BadRequest(new { message = "Promotion name is required." });
        db.Promotions.Add(item); await db.SaveChangesAsync(); return Created($"/api/admin/promotions/{item.Id}", item);
    }

    [HttpGet("{promotionId:guid}")]
    public async Task<IActionResult> Get(Guid promotionId)
    {
        var item = await db.Promotions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == promotionId);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPut("{promotionId:guid}")]
    public async Task<IActionResult> Update(Guid promotionId, PromotionRequest request)
    {
        var item = await db.Promotions.SingleOrDefaultAsync(x => x.Id == promotionId);
        if (item is null) return NotFound();
        var error = ValidateDiscount(request.DiscountType, request.Value, request.MinimumOrderAmount, request.StartsAt, request.EndsAt, request.UsageLimit);
        if (error is not null) return BadRequest(new { message = error });
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "Promotion name is required." });
        item.Name = request.Name.Trim(); item.Description = request.Description?.Trim() ?? string.Empty; item.DiscountType = request.DiscountType;
        item.Value = request.Value; item.MinimumOrderAmount = request.MinimumOrderAmount;
        item.EligibleProductIdsJson = System.Text.Json.JsonSerializer.Serialize(request.EligibleProductIds ?? []);
        item.StartsAt = request.StartsAt; item.EndsAt = request.EndsAt; item.UsageLimit = request.UsageLimit;
        item.IsCombinable = request.IsCombinable; item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(); return Ok(item);
    }

    [HttpPatch("{promotionId:guid}/status")]
    public async Task<IActionResult> SetPromotionStatus(Guid promotionId, StatusRequest request)
    {
        var item = await db.Promotions.SingleOrDefaultAsync(x => x.Id == promotionId);
        if (item is null) return NotFound();
        if (request.Active && (item.StartsAt > DateTime.UtcNow || item.EndsAt <= DateTime.UtcNow || item.UsageLimit is int limit && item.UsageCount >= limit))
            return Conflict(new { message = "Promotion dates or usage limit do not allow activation." });
        item.IsActive = request.Active; item.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync();
        return Ok(new { item.Id, item.IsActive, item.UpdatedAt });
    }

    [HttpDelete("{promotionId:guid}")]
    public async Task<IActionResult> Delete(Guid promotionId)
    {
        var item = await db.Promotions.SingleOrDefaultAsync(x => x.Id == promotionId);
        if (item is null) return NotFound();
        if (await db.Coupons.AnyAsync(x => x.PromotionId == promotionId)) return Conflict(new { message = "Promotion is still referenced by coupons." });
        db.Promotions.Remove(item); await db.SaveChangesAsync(); return NoContent();
    }

    [HttpGet("coupons")]
    public async Task<IActionResult> ListCoupons([FromQuery] string? search, [FromQuery] bool? status)
    {
        var query = db.Coupons.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Code.Contains(search));
        if (status.HasValue) query = query.Where(x => x.IsActive == status);
        return Ok(await query.OrderBy(x => x.Code).ToListAsync());
    }

    [HttpPost("coupons")]
    public async Task<IActionResult> CreateCoupon(CouponRequest request)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (code.Length == 0) return BadRequest(new { message = "Coupon code is required." });
        if (await db.Coupons.AnyAsync(x => x.Code == code)) return Conflict(new { message = "Coupon code already exists." });
        var error = ValidateDiscount(request.DiscountType, request.Value, request.MinimumOrderAmount, request.StartsAt, request.EndsAt, request.UsageLimit);
        if (error is not null) return BadRequest(new { message = error });
        if (request.PromotionId.HasValue && !await db.Promotions.AnyAsync(x => x.Id == request.PromotionId)) return BadRequest(new { message = "Promotion does not exist." });
        var item = new Coupon { Id = Guid.NewGuid(), Code = code, PromotionId = request.PromotionId, DiscountType = request.DiscountType,
            Value = request.Value, MinimumOrderAmount = request.MinimumOrderAmount, StartsAt = request.StartsAt, EndsAt = request.EndsAt,
            UsageLimit = request.UsageLimit, IsCombinable = request.IsCombinable, IsActive = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Coupons.Add(item); await db.SaveChangesAsync(); return Created($"/api/admin/promotions/coupons/{item.Id}", item);
    }

    [HttpGet("coupons/{couponId:guid}")]
    public async Task<IActionResult> GetCoupon(Guid couponId)
    {
        var item = await db.Coupons.AsNoTracking().SingleOrDefaultAsync(x => x.Id == couponId);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPut("coupons/{couponId:guid}")]
    public async Task<IActionResult> UpdateCoupon(Guid couponId, CouponRequest request)
    {
        var item = await db.Coupons.SingleOrDefaultAsync(x => x.Id == couponId);
        if (item is null) return NotFound();
        var code = request.Code.Trim().ToUpperInvariant();
        if (code.Length == 0 || await db.Coupons.AnyAsync(x => x.Code == code && x.Id != couponId)) return Conflict(new { message = "Coupon code is empty or already in use." });
        var error = ValidateDiscount(request.DiscountType, request.Value, request.MinimumOrderAmount, request.StartsAt, request.EndsAt, request.UsageLimit);
        if (error is not null) return BadRequest(new { message = error });
        if (request.PromotionId.HasValue && !await db.Promotions.AnyAsync(x => x.Id == request.PromotionId)) return BadRequest(new { message = "Promotion does not exist." });
        item.Code = code; item.PromotionId = request.PromotionId; item.DiscountType = request.DiscountType; item.Value = request.Value;
        item.MinimumOrderAmount = request.MinimumOrderAmount; item.StartsAt = request.StartsAt; item.EndsAt = request.EndsAt;
        item.UsageLimit = request.UsageLimit; item.IsCombinable = request.IsCombinable; item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(); return Ok(item);
    }

    [HttpPatch("coupons/{couponId:guid}/status")]
    public async Task<IActionResult> SetCouponStatus(Guid couponId, StatusRequest request)
    {
        var item = await db.Coupons.SingleOrDefaultAsync(x => x.Id == couponId);
        if (item is null) return NotFound();
        if (request.Active && (item.StartsAt > DateTime.UtcNow || item.EndsAt <= DateTime.UtcNow || item.UsageLimit is int limit && item.UsageCount >= limit)) return Conflict(new { message = "Coupon dates or usage limit do not allow activation." });
        item.IsActive = request.Active; item.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); return Ok(new { item.Id, item.IsActive });
    }

    [HttpGet("coupons/{couponId:guid}/usage")]
    public async Task<IActionResult> CouponUsage(Guid couponId)
    {
        var item = await db.Coupons.AsNoTracking().SingleOrDefaultAsync(x => x.Id == couponId);
        return item is null ? NotFound() : Ok(new { item.Id, item.Code, item.UsageCount, item.UsageLimit, remaining = item.UsageLimit is null ? (int?)null : Math.Max(0, item.UsageLimit.Value - item.UsageCount) });
    }

    [HttpGet("campaigns")]
    public async Task<IActionResult> ListCampaigns() => Ok(await db.Campaigns.AsNoTracking().OrderByDescending(x => x.CreatedAt).ToListAsync());

    [HttpPost("campaigns")]
    public async Task<IActionResult> CreateCampaign(CampaignRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.EndsAt <= request.StartsAt) return BadRequest(new { message = "Campaign name and a valid date range are required." });
        var item = new Campaign { Id = Guid.NewGuid(), Name = request.Name.Trim(), Channel = request.Channel.Trim(), Content = request.Content.Trim(), StartsAt = request.StartsAt, EndsAt = request.EndsAt, Status = "Draft", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Campaigns.Add(item); await db.SaveChangesAsync(); return Created($"/api/admin/promotions/campaigns/{item.Id}", item);
    }

    [HttpPut("campaigns/{campaignId:guid}")]
    public async Task<IActionResult> UpdateCampaign(Guid campaignId, CampaignRequest request)
    {
        var item = await db.Campaigns.SingleOrDefaultAsync(x => x.Id == campaignId);
        if (item is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Name) || request.EndsAt <= request.StartsAt) return BadRequest(new { message = "Campaign name and a valid date range are required." });
        item.Name = request.Name.Trim(); item.Channel = request.Channel.Trim(); item.Content = request.Content.Trim(); item.StartsAt = request.StartsAt; item.EndsAt = request.EndsAt; item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(); return Ok(item);
    }

    [HttpPatch("campaigns/{campaignId:guid}/status")]
    public async Task<IActionResult> SetCampaignStatus(Guid campaignId, CampaignStatusRequest request)
    {
        var item = await db.Campaigns.SingleOrDefaultAsync(x => x.Id == campaignId);
        if (item is null) return NotFound();
        if (request.Status is not ("Draft" or "Scheduled" or "Active" or "Paused" or "Completed")) return BadRequest(new { message = "Invalid campaign status." });
        if (request.Status == "Active" && (item.StartsAt > DateTime.UtcNow || item.EndsAt <= DateTime.UtcNow)) return Conflict(new { message = "Campaign is outside its active date range." });
        item.Status = request.Status; item.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); return Ok(item);
    }

    [HttpGet("banners")]
    public async Task<IActionResult> ListBanners() => Ok(await db.Banners.AsNoTracking().OrderBy(x => x.DisplayOrder).ToListAsync());

    [HttpPost("banners")]
    public async Task<IActionResult> CreateBanner(BannerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.StartsAt.HasValue && request.EndsAt.HasValue && request.EndsAt <= request.StartsAt) return BadRequest(new { message = "Banner title and a valid date range are required." });
        var item = new Banner { Id = Guid.NewGuid(), Title = request.Title.Trim(), ImageUrl = request.ImageUrl, TargetUrl = request.TargetUrl, DisplayOrder = await db.Banners.CountAsync(), StartsAt = request.StartsAt, EndsAt = request.EndsAt, IsActive = request.IsActive, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Banners.Add(item); await db.SaveChangesAsync(); return Created($"/api/admin/promotions/banners/{item.Id}", item);
    }

    [HttpPut("banners/{bannerId:guid}")]
    public async Task<IActionResult> UpdateBanner(Guid bannerId, BannerRequest request)
    {
        var item = await db.Banners.SingleOrDefaultAsync(x => x.Id == bannerId);
        if (item is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Title) || request.StartsAt.HasValue && request.EndsAt.HasValue && request.EndsAt <= request.StartsAt) return BadRequest(new { message = "Banner title and a valid date range are required." });
        item.Title = request.Title.Trim(); item.ImageUrl = request.ImageUrl; item.TargetUrl = request.TargetUrl; item.StartsAt = request.StartsAt; item.EndsAt = request.EndsAt; item.IsActive = request.IsActive; item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(); return Ok(item);
    }

    [HttpPost("banners/{bannerId:guid}/image")]
    public async Task<IActionResult> SetBannerImage(Guid bannerId, ImageRequest request)
    {
        if (!Uri.TryCreate(request.ImageUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")) return BadRequest(new { message = "Provide a valid HTTP or HTTPS image URL." });
        var item = await db.Banners.SingleOrDefaultAsync(x => x.Id == bannerId);
        if (item is null) return NotFound();
        item.ImageUrl = uri.ToString(); item.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); return Ok(new { item.Id, item.ImageUrl });
    }

    [HttpPut("banners/reorder")]
    public async Task<IActionResult> ReorderBanners(ReorderRequest request)
    {
        if (request.BannerIds.Distinct().Count() != request.BannerIds.Count) return BadRequest(new { message = "Banner IDs must be unique." });
        var banners = await db.Banners.Where(x => request.BannerIds.Contains(x.Id)).ToListAsync();
        if (banners.Count != request.BannerIds.Count) return BadRequest(new { message = "Every banner ID must exist." });
        for (var index = 0; index < request.BannerIds.Count; index++) { banners.Single(x => x.Id == request.BannerIds[index]).DisplayOrder = index; banners[index].UpdatedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync(); return Ok(banners.OrderBy(x => x.DisplayOrder));
    }

    [HttpDelete("banners/{bannerId:guid}")]
    public async Task<IActionResult> DeleteBanner(Guid bannerId)
    {
        var item = await db.Banners.SingleOrDefaultAsync(x => x.Id == bannerId);
        if (item is null) return NotFound(); db.Banners.Remove(item); await db.SaveChangesAsync(); return NoContent();
    }

    private static string? ValidateDiscount(string type, decimal value, decimal minimum, DateTime starts, DateTime ends, int? usageLimit)
    {
        if (type is not ("Percent" or "Fixed")) return "Discount type must be Percent or Fixed.";
        if (value <= 0 || type == "Percent" && value > 100) return "Discount value is outside the allowed range.";
        if (minimum < 0 || ends <= starts) return "Minimum order and date range are invalid.";
        if (usageLimit is <= 0) return "Usage limit must be positive when provided.";
        return null;
    }

    public sealed record PromotionRequest(string Name, string DiscountType, decimal Value, DateTime StartsAt, DateTime EndsAt, string? Description = null, decimal MinimumOrderAmount = 0, List<Guid>? EligibleProductIds = null, int? UsageLimit = null, bool IsCombinable = false);
    public sealed record CouponRequest(string Code, string DiscountType, decimal Value, DateTime StartsAt, DateTime EndsAt, Guid? PromotionId = null, decimal MinimumOrderAmount = 0, int? UsageLimit = null, bool IsCombinable = false);
    public sealed record CampaignRequest(string Name, string Channel, string Content, DateTime StartsAt, DateTime EndsAt);
    public sealed record CampaignStatusRequest(string Status);
    public sealed record BannerRequest(string Title, string? ImageUrl = null, string? TargetUrl = null, DateTime? StartsAt = null, DateTime? EndsAt = null, bool IsActive = false);
    public sealed record StatusRequest(bool Active);
    public sealed record ImageRequest(string ImageUrl);
    public sealed record ReorderRequest(List<Guid> BannerIds);
}
