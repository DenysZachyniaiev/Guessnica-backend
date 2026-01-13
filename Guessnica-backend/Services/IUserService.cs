namespace Guessnica_backend.Services;

using Dtos;

public interface IUserService
{
    Task<UserStatsSummaryDto> GetMyStatsAsync(string userId);
    Task<string> SaveAvatarAsync(string userId, IFormFile file, int maxFileSizeBytes=2097152);
}