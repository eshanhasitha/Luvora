using System.ComponentModel.DataAnnotations;

namespace ProductService.DTOs;

public class CreateCategoryRequest
{
    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;
}
