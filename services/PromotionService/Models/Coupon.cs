namespace PromotionService.Models;

public sealed class Coupon
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid? PromotionId { get; set; }
    public string DiscountType { get; set; } = "Percent";
    public decimal Value { get; set; }
    public decimal MinimumOrderAmount { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public int? UsageLimit { get; set; }
    public int UsageCount { get; set; }
    public bool IsCombinable { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
