namespace Guessnica_backend.Controllers;

using Services;
using Models;
using Data;
using Microsoft.AspNetCore.Identity;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("users")]
[Authorize]
public class UserController: ControllerBase
{
    private readonly IUserService _service;
    private readonly UserManager<AppUser> _userManager;
    private readonly AppDbContext _db;
    
    public UserController(
        UserManager<AppUser> userManager,
        IUserService service,
        AppDbContext db)
    {
        _userManager = userManager;
        _service = service;
        _db = db;
    }
    
    
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                     ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new MeResponseDto
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email,
            Roles = roles.ToArray(),
            AvatarUrl = user.AvatarUrl
        });
    }
    
    [HttpGet("me/stats")]
    public async Task<IActionResult> Stats()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!; 
        return Ok(await _service.GetMyStatsAsync(userId));
    }
    
    [HttpGet("me/history")]
    public async Task<ActionResult<List<UserHistoryEntryDto>>> GetHistory()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var history = await _db.UserRiddles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId && ur.AnsweredAt != null)
            .Include(ur => ur.Riddle)
            .ThenInclude(r => r.Location)
            .OrderByDescending(ur => ur.AnsweredAt)
            .Select(ur => new UserHistoryEntryDto
            {
                Id = ur.Id,
                RiddleId = ur.RiddleId,
                AnsweredAt = ur.AnsweredAt!.Value,

                IsCorrect = ur.IsCorrect ?? false,
                Points = ur.Points ?? 0,

                DistanceMeters = ur.DistanceMeters,
                TimeSeconds = ur.TimeSeconds,

                LocationName = ur.Riddle.Location.ShortDescription
            })
            .ToListAsync();

        return Ok(history);
    }
        
    [HttpPost("me/avatar")]
    [RequestSizeLimit(5_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAvatar([FromForm] AvatarUploadRequest request)
    {
        var avatar = request.Avatar;

        if (avatar == null || avatar.Length == 0)
            return BadRequest(new { message = "No file uploaded" });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound(new { message = "User not found" });

        try
        {
            var avatarUrl = await _service.SaveAvatarAsync(userId, avatar);
            user.AvatarUrl = avatarUrl;
            await _userManager.UpdateAsync(user);

            return Ok(new { avatarUrl });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    
    [HttpGet("me/avatar")]
    [Authorize]
    public async Task<IActionResult> GetAvatar()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || string.IsNullOrEmpty(user.AvatarUrl))
            return NotFound(new { message = "Avatar not found" });

        return Ok(new { avatarUrl = user.AvatarUrl });
    }
}