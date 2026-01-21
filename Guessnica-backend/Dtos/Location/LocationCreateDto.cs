using System.ComponentModel.DataAnnotations;

namespace Guessnica_backend.Dtos.Location;

public class LocationCreateDto
{
    [Required]
    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")]
    public decimal Latitude { get; set; }
    [Required]
    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")]
    public decimal Longitude { get; set; }
    [Required]
    public IFormFile Image { get; init; } = null!;
    [MaxLength(200)]
    public string? ShortDescription { get; set; }
}