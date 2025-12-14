using System.ComponentModel.DataAnnotations;

namespace Guessnica_backend.Dtos.Riddle;

public class RiddleUpdateDto
{
    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = null!;

    [Range(1, 3, ErrorMessage = "Difficulty must be 1 (Easy), 2 (Medium) or 3 (Hard).")]
    public int Difficulty { get; set; }

    [Required]
    public int LocationId { get; set; }
    
    [Range(1, 86400, ErrorMessage = "TimeLimitSeconds must be between 1 and 86400.")]
    public int TimeLimitSeconds { get; set; }
    
    [Range(1, 50000, ErrorMessage = "MaxDistanceMeters must be between 1 and 50000.")]
    public int MaxDistanceMeters { get; set; }
}