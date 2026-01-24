namespace Guessnica_backend.Services;

using Data;
using Dtos;
using Microsoft.EntityFrameworkCore;

public class LeaderboardService : ILeaderboardService
{
    private readonly AppDbContext _db;

    public LeaderboardService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<LeaderboardEntryDto>> GetLeaderboardAsync(int days, int count, LeaderboardCategory category)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        
        var aggregated = await _db.UserRiddles
            .Where(ur => ur.AnsweredAt >= since)
            .GroupBy(ur => ur.UserId)
            .Select(g => new
            {
                UserId = g.Key,

                GamesPlayed = g.Count(),

                CorrectAnswers = g.Count(ur => ur.IsCorrect == true),

                TotalPoints = g.Sum(ur => (ur.Points ?? 0)),

                AverageTimeSeconds = g.Average(ur => (double?)ur.TimeSeconds),
            })
            .ToListAsync();
        
        var withAccuracy = aggregated.Select(x => new
        {
            x.UserId,
            x.GamesPlayed,
            x.CorrectAnswers,
            x.TotalPoints,
            x.AverageTimeSeconds,
            Accuracy = x.GamesPlayed == 0 ? (double?)null : (double)x.CorrectAnswers / x.GamesPlayed
        });
        
        IEnumerable<dynamic> ordered = category switch
        {
            LeaderboardCategory.TotalScore =>
                withAccuracy.OrderByDescending(x => (int)x.TotalPoints),

            LeaderboardCategory.Accuracy =>
                withAccuracy.OrderByDescending(x => (double?)(x.Accuracy ?? -1))
                            .ThenByDescending(x => (int)x.CorrectAnswers),

            LeaderboardCategory.GamesPlayed =>
                withAccuracy.OrderByDescending(x => (int)x.GamesPlayed),

            LeaderboardCategory.AverageTime =>
                withAccuracy.OrderBy(x => (double?)(x.AverageTimeSeconds ?? double.MaxValue)),

            _ => withAccuracy.OrderByDescending(x => (int)x.TotalPoints)
        };

        var top = ordered.Take(count).ToList();
        
        var userIds = top.Select(x => (string)x.UserId).ToList();

        var users = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.AvatarUrl })
            .ToListAsync();
        
        var result = top.Select((x, index) =>
        {
            var u = users.FirstOrDefault(z => z.Id == (string)x.UserId);

            return new LeaderboardEntryDto
            {
                Rank = index + 1,
                UserId = (string)x.UserId,
                DisplayName = u?.DisplayName ?? "Unknown",
                AvatarUrl = u?.AvatarUrl,

                TotalPoints = (int)x.TotalPoints,
                CorrectAnswers = (int)x.CorrectAnswers,
                GamesPlayed = (int)x.GamesPlayed,
                AverageTimeSeconds = (double?)x.AverageTimeSeconds,
                Accuracy = (double?)x.Accuracy
            };
        }).ToList();

        return result;
    }

    public async Task<UserRankDto> GetUserRankAsync(string userId, int days, LeaderboardCategory category)
    {
        var since = DateTime.UtcNow.AddDays(-days);

        var aggregated = await _db.UserRiddles
            .Where(ur => ur.AnsweredAt >= since)
            .GroupBy(ur => ur.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                GamesPlayed = g.Count(),
                CorrectAnswers = g.Count(ur => ur.IsCorrect == true),
                TotalPoints = g.Sum(ur => (ur.Points ?? 0)),
                AverageTimeSeconds = g.Average(ur => (double?)ur.TimeSeconds),
            })
            .ToListAsync();

        var withAccuracy = aggregated.Select(x => new
        {
            x.UserId,
            x.GamesPlayed,
            x.CorrectAnswers,
            x.TotalPoints,
            x.AverageTimeSeconds,
            Accuracy = x.GamesPlayed == 0 ? (double?)null : (double)x.CorrectAnswers / x.GamesPlayed
        });
        
        IEnumerable<dynamic> ordered = category switch
        {
            LeaderboardCategory.TotalScore =>
                withAccuracy.OrderByDescending(x => (int)x.TotalPoints),

            LeaderboardCategory.Accuracy =>
                withAccuracy.OrderByDescending(x => (double?)(x.Accuracy ?? -1))
                            .ThenByDescending(x => (int)x.CorrectAnswers),

            LeaderboardCategory.GamesPlayed =>
                withAccuracy.OrderByDescending(x => (int)x.GamesPlayed),

            LeaderboardCategory.AverageTime =>
                withAccuracy.OrderBy(x => (double?)(x.AverageTimeSeconds ?? double.MaxValue)),

            _ => withAccuracy.OrderByDescending(x => (int)x.TotalPoints)
        };

        var ranked = ordered.ToList();

        var totalUsers = ranked.Count;
        var userEntry = ranked.FirstOrDefault(x => (string)x.UserId == userId);

        if (userEntry == null)
        {
            return new UserRankDto
            {
                Rank = null,
                TotalUsers = totalUsers,
                Days = days,
                Category = category,

                TotalPoints = 0,
                CorrectAnswers = 0,
                GamesPlayed = 0,
                AverageTimeSeconds = null,
                Accuracy = null
            };
        }

        var rank = ranked.IndexOf(userEntry) + 1;

        return new UserRankDto
        {
            Rank = rank,
            TotalUsers = totalUsers,
            Days = days,
            Category = category,

            TotalPoints = (int)userEntry.TotalPoints,
            CorrectAnswers = (int)userEntry.CorrectAnswers,
            GamesPlayed = (int)userEntry.GamesPlayed,
            AverageTimeSeconds = (double?)userEntry.AverageTimeSeconds,
            Accuracy = (double?)userEntry.Accuracy
        };
    }
}