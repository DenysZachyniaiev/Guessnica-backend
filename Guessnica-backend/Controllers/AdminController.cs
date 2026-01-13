namespace Guessnica_backend.Controllers;

using Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("admin")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpGet("riddles/stats")]
    public async Task<IActionResult> GetRiddleStats() =>
        Ok(await _adminService.GetRiddleStatsAsync());

    [HttpGet("users/stats")]
    public async Task<IActionResult> GetUserStats() =>
        Ok(await _adminService.GetUserStatsAsync());

    [HttpGet("submissions")]
    public async Task<IActionResult> GetAllSubmissions() =>
        Ok(await _adminService.GetAllSubmissionsAsync());
}
