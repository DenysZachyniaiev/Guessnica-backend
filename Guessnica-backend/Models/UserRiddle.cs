namespace Guessnica_backend.Models;

public class UserRiddle
{
    public int Id { get; set; }

    public string UserId { get; set; } = null!;
    public AppUser User { get; set; } = null!;

    public int RiddleId { get; set; }
    public Riddle Riddle { get; set; } = null!;

    public DateTime AssignedAt { get; set; }
    
    public DateTime? AnsweredAt { get; set; }
    public bool? IsCorrect { get; set; }
    
    public double? DistanceMeters { get; set; }
    public int? TimeSeconds { get; set; }
    public decimal SubmittedLatitude { get; set; }
    public decimal SubmittedLongitude { get; set; }
    public int? Points { get; set; }
}