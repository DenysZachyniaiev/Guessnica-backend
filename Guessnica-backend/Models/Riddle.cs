namespace Guessnica_backend.Models;

public enum RiddleDifficulty
{
    Easy = 1,
    Medium = 2,
    Hard = 3
}

public class Riddle
{
    public int Id { get; set; }

    public required string Description { get; set; }

    public RiddleDifficulty Difficulty { get; set; }
    
    public int TimeLimitSeconds { get; set; }
    public int MaxDistanceMeters { get; set; }
    public required int LocationId { get; set; }
    public Location Location { get; set; } = null!;
}