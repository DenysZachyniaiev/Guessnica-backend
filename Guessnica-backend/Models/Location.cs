using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Guessnica_backend.Models;

public class Location
{
    public int Id { get; set; }
    [Required]
    [Column(TypeName = "decimal(10,7)")]
    [Range(-90, 90)]
    public decimal Latitude { get; set; }

    [Required]
    [Column(TypeName = "decimal(10,7)")]
    [Range(-180, 180)]
    public decimal Longitude { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    [MaxLength(200)]
    public string? ShortDescription { get; set; }
}