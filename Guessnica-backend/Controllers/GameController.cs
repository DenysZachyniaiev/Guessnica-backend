using Guessnica_backend.Models;

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

        UserRiddle ur;

        try
        {
            ur = await _game.GetDailyRiddleAsync(userId);
        }
        catch (InvalidOperationException e)
        {
            return Conflict(new
            {
                message = e.Message
            });
        }

        return Ok(new DailyRiddleResponseDto
        {
            UserRiddleId = ur.Id,
            RiddleId = ur.RiddleId,
            ImageUrl = ur.Riddle.Location.ImageUrl,
            Description = ur.Riddle.Description,
            Difficulty = (int)ur.Riddle.Difficulty,
            TimeLimitSeconds = ur.Riddle.TimeLimitSeconds,
            MaxDistanceMeters = ur.Riddle.MaxDistanceMeters,
            IsAnswered = ur.AnsweredAt != null
        });
    }
    
    [HttpPost("answer")]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> SubmitAnswer([FromBody] SubmitAnswerDto dto)
    {
        var userId = User?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                     ?? User?.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);

        if (userId == null)
            return Unauthorized();

        try
        {
            var result = await _game.SubmitDailyAnswerAsync(userId, dto.Latitude, dto.Longitude);
            return Ok(new
            {
                result.Points,
                result.DistanceMeters,
                result.TimeSeconds,
                result.IsCorrect
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}