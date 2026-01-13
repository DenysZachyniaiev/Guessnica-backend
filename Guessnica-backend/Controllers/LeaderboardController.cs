namespace Guessnica_backend.Controllers;

using Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    public async Task<IActionResult> Get([FromQuery] int days = 7, [FromQuery] int count = 10) 
    {
        var result = await _service.GetLeaderboardAsync(days, count);
        return Ok(result);
    }
}
