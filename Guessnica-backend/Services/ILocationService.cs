using Guessnica_backend.Models;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Guessnica_backend.Services
{
    public interface ILocationService
    {
        Task<IEnumerable<Location>> GetAllAsync();
        Task<Location> GetByIdAsync(int id);
        
        Task<Location> CreateAsync(Location loc, IFormFile image);
        
        Task<Location> UpdateAsync(int id, Location updated, IFormFile? image = null);

        Task<bool> DeleteAsync(int id);
        
        Task<int> CleanupUnusedImagesAsync();
    }
}