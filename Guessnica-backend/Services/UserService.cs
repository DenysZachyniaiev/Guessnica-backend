namespace Guessnica_backend.Services;

using Data;
using Dtos;
using Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly IWebHostEnvironment _env;

    public UserService(AppDbContext db, UserManager<AppUser> userManager, IWebHostEnvironment env)
    {
        _db = db;
        _userManager = userManager;
        _env = env;
    }

    public async Task<UserStatsSummaryDto> GetMyStatsAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId)
                   ?? throw new InvalidOperationException("User not found");

        var riddles = await _db.UserRiddles
            .Where(ur => ur.UserId == userId)
            .OrderBy(ur => ur.AssignedAt)
            .ToListAsync();

        var answered = riddles.Where(r => r.AnsweredAt != null).ToList();
        var correct = answered.Where(r => r.IsCorrect == true).ToList();
        
        var answeredWithDistance = answered
            .Where(r => r.DistanceMeters != null)
            .ToList();

        var totalDistanceMeters = answeredWithDistance.Sum(r => r.DistanceMeters!.Value);
        var avgDistanceMeters = answeredWithDistance.Any()
            ? answeredWithDistance.Average(r => r.DistanceMeters!.Value)
            : 0;
        
        int current = 0, best = 0;
        foreach (var r in answered.OrderBy(r => r.AnsweredAt))
        {
            if (r.IsCorrect == true)
            {
                current++;
                best = Math.Max(best, current);
            }
            else
            {
                current = 0;
            }
        }

        return new UserStatsSummaryDto
        {
            Assigned = riddles.Count,
            Answered = answered.Count,
            Correct = correct.Count,
            Incorrect = answered.Count - correct.Count,

            TotalScore = correct.Sum(r => r.Points ?? 0),
            AvgScore = correct.Any() ? correct.Average(r => r.Points ?? 0) : 0,

            CurrentStreak = current,
            BestStreak = best,

            AccountCreatedAt = user.CreatedAt,

            TotalDistanceMeters = totalDistanceMeters,
            AvgDistanceMeters = avgDistanceMeters
        };
    }

    public async Task<string> SaveAvatarAsync(string userId, IFormFile file, int maxFileSizeBytes = 2 * 1024 * 1024)
    {
        if (file.Length > maxFileSizeBytes)
            throw new Exception($"File too large. Maximum allowed is {maxFileSizeBytes / (1024 * 1024)} MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png"))
            throw new Exception("Invalid image type");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        ms.Position = 0;

        using var image = Image.Load<Rgba32>(ms);
        if (image.Width != image.Height)
            throw new Exception("Avatar must be square (1:1 aspect ratio)");

        var fileName = $"{Guid.NewGuid()}{ext}";
        var folder = Path.Combine(_env.WebRootPath, "images", "avatars");
        Directory.CreateDirectory(folder);

        var filePath = Path.Combine(folder, fileName);

        ms.Position = 0;
        await using var fileStream = new FileStream(filePath, FileMode.Create);
        await ms.CopyToAsync(fileStream);

        return $"/images/avatars/{fileName}";
    }
}