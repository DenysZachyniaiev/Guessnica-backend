namespace Guessnica_backend.Controllers;

using Dtos;
using Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

[ApiController]
[Route("game")]
[Authorize]
public class GameController : ControllerBase
{
    private readonly IGameService _game;

    public GameController(IGameService game)
    {
        _game = game;
    }

    [HttpGet("daily")]
    public async Task<IActionResult> GetDaily()
    {
        var userId =
            User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId == null)
            return Unauthorized();

        var ur = await _game.GetDailyRiddleAsync(userId);

        return Ok(new DailyRiddleResponseDto
        {
            UserRiddleId = ur.Id,
            RiddleId = ur.RiddleId,
            ImageUrl = ur.Riddle.Location.ImageUrl,
            Description = ur.Riddle.Description,
            Difficulty = (int)ur.Riddle.Difficulty,
            TimeLimitSeconds = ur.Riddle.TimeLimitSeconds,
            MaxDistanceMeters = ur.Riddle.MaxDistanceMeters
        });
    }
}