namespace Guessnica_backend.Dtos;

public class LeaderboardEntryDto
{
    public string UserId { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public int TotalScore { get; set; }
}
