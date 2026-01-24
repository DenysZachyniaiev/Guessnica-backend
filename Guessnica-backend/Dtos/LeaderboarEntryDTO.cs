namespace Guessnica_backend.Dtos;

public class LeaderboardEntryDto
{
    public int Rank { get; set; }
    public string UserId { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? AvatarUrl { get; set; }

    public int TotalPoints { get; set; }
    public int CorrectAnswers { get; set; }

    public int GamesPlayed { get; set; }
    public double? AverageTimeSeconds { get; set; }
    public double? Accuracy { get; set; }
}

public enum LeaderboardCategory
{
    TotalScore,
    Accuracy,
    GamesPlayed,
    AverageTime
}