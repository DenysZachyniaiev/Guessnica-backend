namespace Guessnica_backend.Services;

using Microsoft.EntityFrameworkCore;
using Data;
using Dtos;
using Models;
using Microsoft.AspNetCore.Identity;

public class LeaderboardService : ILeaderboardService
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public LeaderboardService(AppDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<List<LeaderboardEntryDto>> GetLeaderboardAsync(int days, int count)
    {
        var fromDate = DateTime.UtcNow.AddDays(-days);

        var query = await _db.UserRiddles
            .Where(ur =>
                ur.AnsweredAt != null &&
                ur.AnsweredAt >= fromDate &&
                ur.Points != null
            )
            .GroupBy(ur => ur.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalScore = g.Sum(x => x.Points!.Value)
            })
            .OrderByDescending(x => x.TotalScore)
            .Take(count)
            .ToListAsync();
        
        var result = new List<LeaderboardEntryDto>();

        foreach (var row in query)
        {
            var user = await _userManager.FindByIdAsync(row.UserId);
            if (user == null) continue;

            result.Add(new LeaderboardEntryDto
            {
                UserId = user.Id,
                DisplayName = user.DisplayName,
                TotalScore = row.TotalScore
            });
        }

        return result;
    }
}
