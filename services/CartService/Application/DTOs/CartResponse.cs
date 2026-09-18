namespace CartService.DTOs;

public class CartResponse
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public List<CartItemResponse> Items { get; set; } = new();

    public int TotalItems { get; set; }

    public decimal TotalAmount { get; set; }
}