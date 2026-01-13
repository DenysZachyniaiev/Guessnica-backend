namespace Guessnica_backend.Services;

using Dtos.AdminStats;

public interface IAdminService
{
    Task<List<RiddleStatsDto>> GetRiddleStatsAsync();
    Task<List<UserStatsDto>> GetUserStatsAsync();
    Task<List<UserRiddleSubmissionDto>> GetAllSubmissionsAsync();

}