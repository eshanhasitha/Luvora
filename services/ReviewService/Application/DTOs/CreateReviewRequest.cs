using System.ComponentModel.DataAnnotations;

namespace ReviewService.DTOs;

public class CreateReviewRequest
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    public Guid ProductId { get; set; }

    [Required]
    public Guid OrderId { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Comment { get; set; } = string.Empty;
}