namespace Guessnica_backend.Dtos.Riddle;

public class RiddleResponseDto
{
    public int Id { get; set; }
    public required string Description { get; set; }
    public int Difficulty { get; set; }

    public int LocationId { get; set; }

    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public required string ImageUrl { get; set; }
    public string? ShortDescription { get; set; }
    public int TimeLimitSeconds { get; set; }
    public int MaxDistanceMeters { get; set; }
}