using Guessnica_backend.Data;
using Guessnica_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Guessnica_backend.Services;

public class RiddleService : IRiddleService
{
    private readonly AppDbContext _db;

    public RiddleService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<Riddle>> GetAllAsync()
    {
        return await _db.Riddles
            .Include(r => r.Location)
            .ToListAsync();
    }

    public async Task<Riddle?> GetByIdAsync(int id)
    {
        return await _db.Riddles
            .Include(r => r.Location)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<Riddle?> CreateAsync(Riddle riddle)
    {
        // Walidacja: czy taka lokalizacja w ogóle istnieje?
        var locExists = await _db.Locations.AnyAsync(l => l.Id == riddle.LocationId);
        if (!locExists)
            return null; // kontroler zamieni to na 400 BadRequest

        _db.Riddles.Add(riddle);
        await _db.SaveChangesAsync();

        // Dociągamy z bazy z nawigacją Location, żeby w kontrolerze nie było NullReference
        return await _db.Riddles
            .Include(r => r.Location)
            .FirstAsync(r => r.Id == riddle.Id);
    }

    public async Task<Riddle?> UpdateAsync(int id, Riddle updated)
    {
        var existing = await _db.Riddles.FindAsync(id);
        if (existing == null) return null;

        var locExists = await _db.Locations.AnyAsync(l => l.Id == updated.LocationId);
        if (!locExists)
            throw new InvalidOperationException("Location not found");

        existing.Description = updated.Description;
        existing.Difficulty = updated.Difficulty;
        existing.LocationId = updated.LocationId;

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var existing = await _db.Riddles.FindAsync(id);
        if (existing == null) return false;

        _db.Riddles.Remove(existing);
        await _db.SaveChangesAsync();
        return true;
    }
}