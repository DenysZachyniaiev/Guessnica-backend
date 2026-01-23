namespace Guessnica_backend.Controllers;

using Services;
using Models;
using Microsoft.AspNetCore.Identity;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("users")]
[Authorize]
public class UserController: ControllerBase
{
    private readonly IUserService _service;
    private readonly UserManager<AppUser> _userManager;
    
    public UserController(UserManager<AppUser> userManager,IUserService service)
    {
        _userManager = userManager;
        _service = service;
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

        // Dodano URL avatara z UserController!
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
        
    [HttpPost("me/avatar")]
    public async Task<IActionResult> UploadAvatar(IFormFile avatar)
    {
        if (avatar == null || avatar.Length == 0)
            return BadRequest("No file uploaded");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await _userManager.FindByIdAsync(userId);

        try
        {
            var avatarUrl = await _service.SaveAvatarAsync(user.Id, avatar);
            user.AvatarUrl = avatarUrl;
            await _userManager.UpdateAsync(user);

            return Ok(new { avatarUrl });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}