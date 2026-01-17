using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Guessnica_backend.Dtos;
using Guessnica_backend.Models;
using Guessnica_backend.Services;

[ApiController]
[Route("auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly IJwtService _jwtService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        IJwtService jwtService,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtService = jwtService;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        if (!ModelState.IsValid)
            return new BadRequestObjectResult(new ValidationProblemDetails(ModelState));

        try
        {
            if (string.IsNullOrWhiteSpace(dto?.Email))
            {
                _logger.LogWarning("Login attempt with null or empty email");
                return Unauthorized("Invalid credentials");
            }

            var email = dto.Email.Trim().ToLowerInvariant();
            var user = await _userManager.FindByEmailAsync(email);

            if (user == null || !user.EmailConfirmed)
            {
                _logger.LogWarning("Login failed for email: {Email} - User not found or email not confirmed", email);
                return Unauthorized("Invalid credentials or email not confirmed");
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, false);
            if (!result.Succeeded)
            {
                _logger.LogWarning("Login failed for user: {UserId} - Invalid password", user.Id);
                return Unauthorized("Invalid credentials");
            }

            try
            {
                var token = await _jwtService.GenerateTokenAsync(user);
                _logger.LogInformation("User {UserId} logged in successfully", user.Id);
                return Ok(token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "JWT generation failed for user {UserId}", user.Id);
                return StatusCode(500, "An error occurred during authentication. Please try again later.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login");
            return StatusCode(500, "An error occurred during authentication. Please try again later.");
        }
    }

    [AllowAnonymous]
    [HttpPost("facebook")]
    public async Task<IActionResult> FacebookLogin([FromBody] FacebookLoginDto dto,
        [FromServices] IFacebookAuthService fb)
    {
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Facebook login attempt with invalid model state");
            return new BadRequestObjectResult(new ValidationProblemDetails(ModelState));
        }

        try
        {
            var token = await fb.HandleFacebookLoginAsync(dto, _userManager, _jwtService);

            if (token == null)
            {
                _logger.LogWarning("Facebook authentication failed - null token returned");
                return Unauthorized(new { message = "Facebook authentication failed." });
            }

            _logger.LogInformation("Facebook login successful");
            return Ok(token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Facebook authentication error");
            return StatusCode(500, "An error occurred during authentication. Please try again later.");
        }
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        try
        {
            var userId = User?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                         ?? User?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                _logger.LogWarning("Me endpoint called with no user ID in claims");
                return Unauthorized();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("User not found for ID: {UserId}", userId);
                return NotFound();
            }

            var roles = await _userManager.GetRolesAsync(user);

            _logger.LogDebug("User info retrieved for: {UserId}", userId);

            return Ok(new MeResponseDto
            {
                Id = user.Id,
                DisplayName = user.DisplayName,
                Email = user.Email,
                Roles = roles.ToArray()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user information");
            return StatusCode(500, "An error occurred while retrieving user information.");
        }
    }

    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        var userId = User?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        
        if (User == null)
        {
            _logger.LogWarning("Logout called with null User (test environment?)");
        }
        else
        {
            _logger.LogInformation("User {UserId} logged out", userId);
        }

        return Ok(new { message = "Logged out." });
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto,
        [FromServices] IAppEmailSender emailSender)
    {
        if (!ModelState.IsValid)
            return new BadRequestObjectResult(new ValidationProblemDetails(ModelState));

        try
        {
            if (string.IsNullOrWhiteSpace(dto?.Email))
            {
                _logger.LogWarning("Registration attempt with null or empty email");
                return new BadRequestObjectResult(new ValidationProblemDetails(ModelState));
            }

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
                    _logger.LogWarning("User creation failed for email: {Email}. Errors: {Errors}",
                        email, string.Join(", ", result.Errors.Select(e => e.Description)));

                    return BadRequest(new
                    {
                        message = "Registration failed",
                        errors = result.Errors.Select(e => new { e.Code, e.Description })
                    });
                }

                var roleResult = await _userManager.AddToRoleAsync(user, "User");
                if (!roleResult.Succeeded)
                {
                    _logger.LogError("Failed to assign role to user {UserId}. Errors: {Errors}",
                        user.Id, string.Join(", ", roleResult.Errors.Select(e => e.Description)));

                    return StatusCode(500, new
                    {
                        message = "Registration failed. Please try again later."
                    });
                }

                try
                {
                    var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    var confirmationLink = Url.Action("ConfirmEmail", "Auth",
                        new { userId = user.Id, token }, Request.Scheme);

                    await emailSender.SendAsync(email, "Confirm your Guessnica account",
                        $"Welcome! Please confirm your email by clicking this link: {confirmationLink}");

                    _logger.LogInformation("Confirmation email sent to new user: {Email}", email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send confirmation email to {Email}", email);

                    return StatusCode(500, new
                    {
                        message = "Registration completed but email could not be sent. Please contact support."
                    });
                }
            }
            else
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));

                _logger.LogInformation("Registration attempt for existing email: {Email}", email);

                try
                {
                    await emailSender.SendAsync(email, "Guessnica registration attempt",
                        "Someone tried to register a Guessnica account using your email.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send notification email to existing user: {Email}", email);
                }
            }

            return Ok(new { message = "If the email is valid, instructions have been sent." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during registration");

            return StatusCode(500, new
            {
                message = "An error occurred during registration. Please try again later."
            });
        }
    }

    [AllowAnonymous]
    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(string userId, string token)
    {
        try
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Email confirmation attempted with missing userId or token");
                return BadRequest(new { message = "Invalid confirmation link." });
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("Email confirmation failed - user not found: {UserId}", userId);
                return BadRequest(new { message = "Invalid confirmation link." });
            }

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (!result.Succeeded)
            {
                _logger.LogWarning("Email confirmation failed for user {UserId}. Errors: {Errors}",
                    userId, string.Join(", ", result.Errors.Select(e => e.Description)));
                return BadRequest(new { message = "Invalid confirmation link." });
            }

            _logger.LogInformation("Email confirmed successfully for user: {UserId}", userId);
            return Ok(new { message = "Email confirmed successfully!" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during email confirmation");

            return StatusCode(500, new
            {
                message = "An error occurred during email confirmation. Please try again later."
            });
        }
    }
}