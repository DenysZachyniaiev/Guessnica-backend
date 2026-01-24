namespace Guessnica_backend.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;
using System.Security.Claims;
using Dtos;

[ApiController]
[Route("leaderboard")]
public class LeaderboardController : ControllerBase
{
    private readonly ILeaderboardService _service;

    public LeaderboardController(ILeaderboardService service)
    {
        _service = service;
    }
    
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<LeaderboardEntryDto>>> Get(
        [FromQuery] int days = 7,
        [FromQuery] int count = 10,
        [FromQuery] LeaderboardCategory category = LeaderboardCategory.TotalScore
    )
    {
        if (days < 1) days = 1;
        if (days > 3650) days = 3650;

        if (count < 1) count = 1;
        if (count > 200) count = 200;

        var result = await _service.GetLeaderboardAsync(days, count, category);
        return Ok(result);
    }
    
    [HttpGet("rank")]
    [Authorize]
    public async Task<ActionResult<UserRankDto>> GetUserRank(
        [FromQuery] int days = 7,
        [FromQuery] LeaderboardCategory category = LeaderboardCategory.TotalScore
    )
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (days < 1) days = 1;
        if (days > 3650) days = 3650;

        var rank = await _service.GetUserRankAsync(userId, days, category);
        return Ok(rank);
    }
}