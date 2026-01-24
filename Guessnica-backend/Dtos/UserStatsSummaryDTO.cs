namespace Guessnica_backend.Dtos;

public class UserStatsSummaryDto
{
    public int Assigned { get; set; }
    public int Answered { get; set; }
    public int Correct { get; set; }
    public int Incorrect { get; set; }

    public int TotalScore { get; set; }
    public double AvgScore { get; set; }

    public int CurrentStreak { get; set; }
    public int BestStreak { get; set; }

    public DateTime AccountCreatedAt { get; set; }
    
    public double TotalDistanceMeters { get; set; }
    public double AvgDistanceMeters { get; set; }
}