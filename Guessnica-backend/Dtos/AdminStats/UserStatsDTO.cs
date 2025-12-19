namespace Guessnica_backend.Dtos.AdminStats;

public class UserStatsDto
{
    public string UserId { get; set; }
    public string DisplayName { get; set; }
    public int RiddlesAnswered { get; set; }
    public double? TotalScore { get; set; }
    public double? AverageScore { get; set; }
}