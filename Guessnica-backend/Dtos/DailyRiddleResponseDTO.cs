namespace Guessnica_backend.Dtos;

public class DailyRiddleResponseDto
{
    public int UserRiddleId { get; set; }
    public int RiddleId { get; set; }

    public string ImageUrl { get; set; } = null!;
    public string Description { get; set; } = null!;

    public int Difficulty { get; set; }
    public int TimeLimitSeconds { get; set; }
    public double MaxDistanceMeters { get; set; }
}