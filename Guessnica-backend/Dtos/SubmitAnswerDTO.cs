using System.ComponentModel.DataAnnotations;

namespace Guessnica_backend.Dtos;

public class SubmitAnswerDto
{
    [Required]
    public decimal Latitude { get; set; }
    [Required]
    public decimal Longitude { get; set; }
}