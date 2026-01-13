namespace Guessnica_backend.Services;

using Data;
using Microsoft.EntityFrameworkCore;
using Dtos.AdminStats;

public class AdminService : IAdminService
{
     private readonly AppDbContext _db;

    public AdminService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<RiddleStatsDto>> GetRiddleStatsAsync()
    {
        return await _db.UserRiddles
            .Include(ur => ur.Riddle)
            .ThenInclude(r => r.Location)
            .Where(ur => ur.AnsweredAt != null)
            .GroupBy(ur => ur.RiddleId)
            .Select(g => new RiddleStatsDto
            {
                RiddleId = g.Key,
                Description = g.First().Riddle.Description,
                LocationId = g.First().Riddle.LocationId,
                ShortDescription = g.First().Riddle.Location.ShortDescription,
                Latitude = g.First().Riddle.Location.Latitude,
                Longitude = g.First().Riddle.Location.Longitude,
                ImageUrl = g.First().Riddle.Location.ImageUrl,
                TimesAnswered = g.Count(),
                AvgScore = g.Average(ur => ur.Points),
                AvgDistanceMeters = g.Average(ur => ur.DistanceMeters),
                AvgTimeSeconds = g.Average(ur => ur.TimeSeconds)
            })
            .ToListAsync();
    }
    
    public async Task<List<UserStatsDto>> GetUserStatsAsync()
    {
        return await _db.UserRiddles
            .Include(ur => ur.User)
            .Where(ur => ur.AnsweredAt != null)
            .GroupBy(ur => new { ur.UserId, ur.User.DisplayName })
            .Select(g => new UserStatsDto
            {
                UserId = g.Key.UserId,
                DisplayName = g.Key.DisplayName,
                RiddlesAnswered = g.Count(),
                TotalScore = g.Sum(ur => ur.Points),
                AverageScore = g.Average(ur => ur.Points)
            })
            .ToListAsync();
    }
    
    public async Task<List<UserRiddleSubmissionDto>> GetAllSubmissionsAsync()
    {
        return await _db.UserRiddles
            .Include(ur => ur.User)
            .Include(ur => ur.Riddle)
            .ThenInclude(r => r.Location)
            .Where(ur => ur.AnsweredAt != null)
            .Select(ur => new UserRiddleSubmissionDto
            {
                UserId = ur.UserId,
                DisplayName = ur.User.DisplayName,
                RiddleId = ur.RiddleId,
                SubmittedLatitude = ur.SubmittedLatitude,
                SubmittedLongitude = ur.SubmittedLongitude,
                DistanceMeters = ur.DistanceMeters,
                TimeSeconds = ur.TimeSeconds,
                Score = ur.Points
            })
            .ToListAsync();
    }
}