using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication;
using Guessnica_backend.Models;
using Guessnica_backend.Services;
using Guessnica_backend.Dtos;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;

namespace Guessnica_backend.Tests.Controllers;

public class MeTests
{
    private readonly Mock<UserManager<AppUser>> _userManagerMock;
    private readonly Mock<SignInManager<AppUser>> _signInManagerMock;
    private readonly Mock<IJwtService> _jwtServiceMock;
    private readonly Mock<ILogger<AuthController>> _loggerMock;
    private readonly AuthController _controller;
    private readonly Mock<IConfiguration> _configuration;

    public MeTests()
    {
        var userStoreMock = new Mock<IUserStore<AppUser>>();
        
        _userManagerMock = new Mock<UserManager<AppUser>>(
            userStoreMock.Object,
            new Mock<IOptions<IdentityOptions>>().Object,
            new Mock<IPasswordHasher<AppUser>>().Object,
            new[] { new Mock<IUserValidator<AppUser>>().Object },
            new[] { new Mock<IPasswordValidator<AppUser>>().Object },
            new Mock<ILookupNormalizer>().Object,
            new Mock<IdentityErrorDescriber>().Object,
            new Mock<IServiceProvider>().Object,
            new Mock<ILogger<UserManager<AppUser>>>().Object);

        var contextAccessorMock = new Mock<IHttpContextAccessor>();
        var claimsFactoryMock = new Mock<IUserClaimsPrincipalFactory<AppUser>>();
        
        _signInManagerMock = new Mock<SignInManager<AppUser>>(
            _userManagerMock.Object,
            contextAccessorMock.Object,
            claimsFactoryMock.Object,
            new Mock<IOptions<IdentityOptions>>().Object,
            new Mock<ILogger<SignInManager<AppUser>>>().Object,
            new Mock<IAuthenticationSchemeProvider>().Object,
            new Mock<IUserConfirmation<AppUser>>().Object);

        _jwtServiceMock = new Mock<IJwtService>();
        _loggerMock = new Mock<ILogger<AuthController>>();
        _configuration = new Mock<IConfiguration>();

        _controller = new AuthController(
            _userManagerMock.Object,
            _signInManagerMock.Object,
            _jwtServiceMock.Object,
            _loggerMock.Object,
            _configuration.Object);
    }

    [Fact]
    public async Task Me_WithValidUser_ReturnsOkWithUserData()
    {
        var userId = "user-id-123";
        var user = new AppUser
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User"
        };
        var roles = new List<string> { "User", "Admin" };

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId)
        };
        var identity = new ClaimsIdentity(claims);
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };

        _userManagerMock.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _userManagerMock.Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(roles);

        var result = await _controller.Me();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        var responseDto = Assert.IsType<MeResponseDto>(okResult.Value);
        Assert.Equal(userId, responseDto.Id);
        Assert.Equal("Test User", responseDto.DisplayName);
        Assert.Equal("test@example.com", responseDto.Email);
        Assert.Equal(2, responseDto.Roles.Length);
        Assert.Contains("User", responseDto.Roles);
        Assert.Contains("Admin", responseDto.Roles);
    }

    [Fact]
    public async Task Me_WithNameIdentifierClaim_ReturnsOkWithUserData()
    {
        var userId = "user-id-456";
        var user = new AppUser
        {
            Id = userId,
            Email = "test2@example.com",
            DisplayName = "Test User 2"
        };

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId)
        };
        var identity = new ClaimsIdentity(claims);
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };

        _userManagerMock.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _userManagerMock.Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "User" });

        var result = await _controller.Me();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        var responseDto = Assert.IsType<MeResponseDto>(okResult.Value);
        Assert.Equal(userId, responseDto.Id);
    }

    [Fact]
    public async Task Me_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var claims = new List<Claim>();
        var identity = new ClaimsIdentity(claims);
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };

        var result = await _controller.Me();

        var unauthorizedResult = Assert.IsType<UnauthorizedResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
        _userManagerMock.Verify(x => x.FindByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Me_WithNonExistentUser_ReturnsNotFound()
    {
        var userId = "non-existent-user-id";
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId)
        };
        var identity = new ClaimsIdentity(claims);
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };

        _userManagerMock.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(null as AppUser);

        var result = await _controller.Me();

        var notFoundResult = Assert.IsType<NotFoundResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);
    }

    [Fact]
    public async Task Me_WhenGettingRolesFails_ReturnsInternalServerError()
    {
        var userId = "user-id-123";
        var user = new AppUser
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User"
        };

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId)
        };
        var identity = new ClaimsIdentity(claims);
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };

        _userManagerMock.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _userManagerMock.Setup(x => x.GetRolesAsync(user))
            .ThrowsAsync(new System.Exception("Database connection error"));

        var result = await _controller.Me();

        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);

        var message = statusCodeResult.Value?.ToString();
        Assert.DoesNotContain("Database connection error", message);
        Assert.Contains("error occurred", message, System.StringComparison.OrdinalIgnoreCase);
    }
}