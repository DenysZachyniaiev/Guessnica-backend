namespace Guessnica_backend.Dtos.AdminStats;

public class UserRiddleSubmissionDto
{
    public string UserId { get; set; }
    public string DisplayName { get; set; }
    public int RiddleId { get; set; }
    public decimal SubmittedLatitude { get; set; }
    public decimal SubmittedLongitude { get; set; }
    public double? DistanceMeters { get; set; }
    public int? TimeSeconds { get; set; }
    public int? Score { get; set; }
}