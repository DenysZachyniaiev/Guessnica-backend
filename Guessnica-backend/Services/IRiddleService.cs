using Guessnica_backend.Models;

namespace Guessnica_backend.Services;

public interface IRiddleService
{
    Task<IEnumerable<Riddle>> GetAllAsync();
    Task<Riddle?> GetByIdAsync(int id);
    Task<Riddle?> CreateAsync(Riddle riddle);
    Task<Riddle?> UpdateAsync(int id, Riddle updated);
    Task<bool> DeleteAsync(int id);
}