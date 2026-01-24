namespace Guessnica_backend.Dtos;

public class UserHistoryEntryDto
{
    public int Id { get; set; }
    public int RiddleId { get; set; }

    public DateTime AnsweredAt { get; set; }

    public bool IsCorrect { get; set; }
    public int Points { get; set; }

    public double? DistanceMeters { get; set; }
    public int? TimeSeconds { get; set; }

    public string? LocationName { get; set; }
}