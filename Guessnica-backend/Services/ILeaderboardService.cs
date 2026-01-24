namespace Guessnica_backend.Services;

using Guessnica_backend.Dtos;

public interface ILeaderboardService
{
    Task<List<LeaderboardEntryDto>> GetLeaderboardAsync(int days, int count, LeaderboardCategory category);
    Task<UserRankDto> GetUserRankAsync(string userId, int days, LeaderboardCategory category);
}