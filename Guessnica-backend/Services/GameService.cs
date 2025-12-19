namespace Guessnica_backend.Services;

using Helpers;
using Data;
using Models;
using Microsoft.EntityFrameworkCore;

public class GameService : IGameService
{
    private readonly AppDbContext _db;

    public GameService(AppDbContext db)
    {
        _db = db;
    }
    
    public async Task<UserRiddle> GetDailyRiddleAsync(string userId, int dailyHourUtc = 0)
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date.AddHours(dailyHourUtc);
        if (now < todayStart)
        {
            todayStart = todayStart.AddDays(-1);
        }
        var tomorrowStart = todayStart.AddDays(1);
        
        var existing = await _db.UserRiddles
            .Include(ur => ur.Riddle)
            .ThenInclude(r => r.Location)
            .FirstOrDefaultAsync(ur =>
                ur.UserId == userId &&
                ur.AssignedAt >= todayStart &&
                ur.AssignedAt < tomorrowStart
            );

        if (existing != null)
            return existing;
        
        var solvedRiddleIds = await _db.UserRiddles
            .Where(ur => ur.UserId == userId && ur.IsCorrect==true)
            .Select(ur => ur.RiddleId)
            .Distinct()
            .ToListAsync();
        
        var availableRiddles = await _db.Riddles
            .Include(r => r.Location)
            .Where(r => !solvedRiddleIds.Contains(r.Id))
            .ToListAsync();

        if (!availableRiddles.Any())
            throw new InvalidOperationException("No available riddles");
        
        var picked = availableRiddles[Random.Shared.Next(availableRiddles.Count)];

        var userRiddle = new UserRiddle
        {
            UserId = userId,
            RiddleId = picked.Id,
            AssignedAt = now
        };

        _db.UserRiddles.Add(userRiddle);
        await _db.SaveChangesAsync();
        
        return await _db.UserRiddles
            .Include(ur => ur.Riddle)
            .ThenInclude(r => r.Location)
            .FirstAsync(ur => ur.Id == userRiddle.Id);
    }
    
     public async Task<UserRiddle> SubmitDailyAnswerAsync(string userId, decimal latitude, decimal longitude)
    {
        var today = DateTime.UtcNow.Date;
        var userRiddle = await _db.UserRiddles
            .Include(ur => ur.Riddle)
            .ThenInclude(r => r.Location)
            .FirstOrDefaultAsync(ur =>
                ur.UserId == userId &&
                ur.AssignedAt.Date == today
            );

        if (userRiddle == null)
            throw new InvalidOperationException("No riddle assigned today");

        if (userRiddle.AnsweredAt!=null)
            throw new InvalidOperationException("Riddle already answered");

        var location = userRiddle.Riddle.Location;
        
        double distanceMeters = HaversineDistance.CalculateDistance(
            latitude, longitude,
            location.Latitude, location.Longitude
        );
        
        int timeElapsedSeconds = (int)(DateTime.UtcNow - userRiddle.AssignedAt).TotalSeconds;
        
        bool isCorrect = distanceMeters <= userRiddle.Riddle.MaxDistanceMeters;
        
        int score = ScoreCalculation.CalculateScore(
            basePoints: (int)userRiddle.Riddle.Difficulty,
            distanceMeters: distanceMeters,
            timeSeconds: timeElapsedSeconds,
            maxDistance: userRiddle.Riddle.MaxDistanceMeters
        );
        
        userRiddle.DistanceMeters = distanceMeters;
        userRiddle.TimeSeconds = timeElapsedSeconds;
        userRiddle.Points = score;
        userRiddle.IsCorrect = isCorrect;
        userRiddle.AnsweredAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return userRiddle;
    }
}
