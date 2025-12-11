using System.ComponentModel.DataAnnotations;

namespace Guessnica_backend.Dtos.Riddle;

public class RiddleCreateDto
{
    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = null!;

    [Range(1, 3, ErrorMessage = "Difficulty must be 1 (Easy), 2 (Medium) or 3 (Hard).")]
    public int Difficulty { get; set; }

    [Required]
    public int LocationId { get; set; }
}