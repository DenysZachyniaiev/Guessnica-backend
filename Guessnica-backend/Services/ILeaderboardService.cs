namespace Guessnica_backend.Services;

using Dtos;

public interface ILeaderboardService
{
    Task<List<LeaderboardEntryDto>> GetLeaderboardAsync(int days, int count);
}