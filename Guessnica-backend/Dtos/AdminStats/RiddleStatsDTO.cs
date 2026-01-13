namespace Guessnica_backend.Dtos.AdminStats;

public class RiddleStatsDto
{
    public int RiddleId { get; set; }
    public string Description { get; set; }
    public int LocationId { get; set; }
    public string ShortDescription { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string ImageUrl { get; set; }
    public int TimesAnswered { get; set; }
    public double? AvgScore { get; set; }
    public double? AvgDistanceMeters { get; set; }
    public double? AvgTimeSeconds { get; set; }
}