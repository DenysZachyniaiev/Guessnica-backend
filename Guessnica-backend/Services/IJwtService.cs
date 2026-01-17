namespace Guessnica_backend.Services;

using Dtos;
using Models;
using System.Threading.Tasks;

public interface IJwtService
{
    Task<TokenResponseDto> GenerateTokenAsync(AppUser user);
}