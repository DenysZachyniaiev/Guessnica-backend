using Guessnica_backend.Data;
using Guessnica_backend.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Guessnica_backend.Services;

public class LocationService : ILocationService
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public LocationService(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public async Task<IEnumerable<Location>> GetAllAsync()
    {
        return await _db.Locations.ToListAsync();
    }

    public async Task<Location> GetByIdAsync(int id)
    {
        var location = await _db.Locations
            .FirstOrDefaultAsync(l => l.Id == id);
        
        if (location == null)
            throw new KeyNotFoundException($"Location with id {id} not found");
        
        return location;
    }

    public async Task<Location> CreateAsync(Location loc, IFormFile image)
    {
        if (image == null || image.Length == 0)
            throw new ArgumentException("Image is required", nameof(image));

        loc.ImageUrl = await SaveImageAsync(Guid.NewGuid().ToString(), image);
        _db.Locations.Add(loc);
        await _db.SaveChangesAsync();
        return loc;
    }

    public async Task<Location> UpdateAsync(int id, Location updated, IFormFile? image = null)
    {
        var loc = await _db.Locations.FindAsync(id);
        if (loc == null)
            throw new KeyNotFoundException($"Location with id {id} not found");

        loc.Latitude = updated.Latitude;
        loc.Longitude = updated.Longitude;
        loc.ShortDescription = updated.ShortDescription;

        if (image != null && image.Length > 0)
        {
            DeleteImage(loc.ImageUrl);
            loc.ImageUrl = await SaveImageAsync(Guid.NewGuid().ToString(), image);
        }

        await _db.SaveChangesAsync();
        return loc;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var loc = await _db.Locations.FindAsync(id);
        if (loc == null) return false;

        if (!string.IsNullOrWhiteSpace(loc.ImageUrl))
        {
            DeleteImage(loc.ImageUrl);
        }

        _db.Locations.Remove(loc);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<int> CleanupUnusedImagesAsync()
    {
        var folder = Path.Combine(_env.WebRootPath, "images/locations");
        if (!Directory.Exists(folder))
            return 0;

        var allFiles = Directory.GetFiles(folder);
        var usedUrls = await _db.Locations
            .Where(l => !string.IsNullOrEmpty(l.ImageUrl))
            .Select(l => Path.GetFileName(l.ImageUrl))
            .ToListAsync();

        int deletedCount = 0;
        foreach (var file in allFiles)
        {
            var fileName = Path.GetFileName(file);
            if (!usedUrls.Contains(fileName))
            {
                File.Delete(file);
                deletedCount++;
            }
        }

        return deletedCount;
    }
    
    private async Task<string> SaveImageAsync(string fileKey, IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLower();
        if (ext != ".jpg" && ext != ".png" && ext != ".jpeg")
            throw new Exception("Invalid image type");

        var fileName = $"{fileKey}{ext}";
        var folder = Path.Combine(_env.WebRootPath, "images/locations");
        Directory.CreateDirectory(folder);

        var filePath = Path.Combine(folder, fileName);
        using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        return $"/images/locations/{fileName}";
    }

    private void DeleteImage(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return;

        var path = Path.Combine(_env.WebRootPath, imageUrl.TrimStart('/'));
        if (File.Exists(path))
            File.Delete(path);
    }
}