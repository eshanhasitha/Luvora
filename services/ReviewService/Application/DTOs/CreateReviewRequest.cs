using System.ComponentModel.DataAnnotations;

namespace ReviewService.DTOs;

public class CreateReviewRequest
{
    public Guid? UserId { get; set; }

    public Guid? ProductId { get; set; }

    public Guid? OrderId { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }

    [MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Comment { get; set; } = string.Empty;
}
