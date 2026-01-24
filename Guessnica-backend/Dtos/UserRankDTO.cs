namespace Guessnica_backend.Dtos;

public class UserRankDto
{
    public int? Rank { get; set; }
    public int TotalUsers { get; set; }
    public int Days { get; set; }
    public LeaderboardCategory Category { get; set; }
    
    public int TotalPoints { get; set; }
    public int CorrectAnswers { get; set; }
    public int GamesPlayed { get; set; }
    public double? AverageTimeSeconds { get; set; }
    public double? Accuracy { get; set; }
}