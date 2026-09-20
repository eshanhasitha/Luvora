namespace ReviewService.DTOs;

public class ReviewResponse
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid ProductId { get; set; }

    public Guid OrderId { get; set; }

    public int Rating { get; set; }

    public string Comment { get; set; } = string.Empty;

    public bool IsApproved { get; set; }

    public DateTime CreatedAt { get; set; }
}