namespace Guessnica_backend.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using Dtos;
using Models;
using Services;

[ApiController]
[Route("auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly IJwtService _jwtService;

    public AuthController(UserManager<AppUser> userManager,
                          SignInManager<AppUser> signInManager,
                          IJwtService jwtService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtService = jwtService;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || !user.EmailConfirmed)
            return Unauthorized("Invalid credentials or email not confirmed");

        var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, false);
        if (!result.Succeeded)
            return Unauthorized("Invalid credentials");

        var token = await _jwtService.GenerateTokenAsync(user);
        return Ok(token);
    }

    [AllowAnonymous]
    [HttpPost("facebook")]
    public async Task<IActionResult> FacebookLogin([FromBody] FacebookLoginDto dto,
        [FromServices] IFacebookAuthService fb)
    {
        var token = await fb.HandleFacebookLoginAsync(dto, _userManager, _jwtService);
        return Ok(token);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        Console.WriteLine("aaa");
        Console.WriteLine(User.FindFirstValue(JwtRegisteredClaimNames.Sub));
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                     ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new MeResponseDto
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email,
            Roles = roles.ToArray()
        });
    }

    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout() => Ok(new { message = "Logged out (discard JWT client-side)." });
    
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto,
        [FromServices] IAppEmailSender emailSender)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(email);

        if (user == null)
        {
            user = new AppUser
            {
                UserName = email,
                Email = email,
                DisplayName = dto.DisplayName,
                EmailConfirmed = false
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    message = "Registration failed",
                    errors = result.Errors.Select(e => new { e.Code, e.Description })
                });
            }

            await _userManager.AddToRoleAsync(user, "User");
            
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmationLink = Url.Action("ConfirmEmail", "Auth",
                new { userId = user.Id, token }, Request.Scheme);

            await emailSender.SendAsync(email, "Confirm your Guessnica account",
                $"Welcome! Please confirm your email by clicking this link: {confirmationLink}");
        }
        else
        {
            await emailSender.SendAsync(email, "Guessnica registration attempt",
                "Someone tried to register a Guessnica account using your email. " +
                "If this wasn't you, no action is required. Your account remains safe.");
        }
        
        return Ok(new { message = "If the email is valid, instructions have been sent." });
    }
    
    [AllowAnonymous]
    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(string userId, string token)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
            return BadRequest();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded) return BadRequest(new { message = "Email confirmation failed." });

        return Ok(new { message = "Email confirmed successfully!" });
    }
}
