using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Guessnica_backend.Controllers;
using Guessnica_backend.Models;
using Guessnica_backend.Services;
using Guessnica_backend.Dtos;

namespace Guessnica_backend.Tests.Controllers;

public class LoginTests
{
    private readonly Mock<UserManager<AppUser>> _userManagerMock;
    private readonly Mock<SignInManager<AppUser>> _signInManagerMock;
    private readonly Mock<IJwtService> _jwtServiceMock;
    private readonly Mock<ILogger<AuthController>> _loggerMock;
    private readonly AuthController _controller;

    public LoginTests()
    {
        var userStoreMock = new Mock<IUserStore<AppUser>>();
        _userManagerMock = new Mock<UserManager<AppUser>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);

        var contextAccessorMock = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var claimsFactoryMock = new Mock<IUserClaimsPrincipalFactory<AppUser>>();
        _signInManagerMock = new Mock<SignInManager<AppUser>>(
            _userManagerMock.Object, contextAccessorMock.Object,
            claimsFactoryMock.Object, null, null, null, null);

        _jwtServiceMock = new Mock<IJwtService>();
        _loggerMock = new Mock<ILogger<AuthController>>();

        _controller = new AuthController(
            _userManagerMock.Object,
            _signInManagerMock.Object,
            _jwtServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithToken()
    {
        var dto = new LoginDto { Email = "test@example.com", Password = "Password123!" };
        var user = new AppUser
        {
            Id = "user-id",
            Email = "test@example.com",
            EmailConfirmed = true
        };
        var expectedToken = new TokenResponseDto
        {
            Token = "jwt-token-string",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync("test@example.com"))
            .ReturnsAsync(user);
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, dto.Password, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _jwtServiceMock.Setup(x => x.GenerateTokenAsync(user))
            .ReturnsAsync(expectedToken);

        var result = await _controller.Login(dto);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        var tokenResponse = Assert.IsType<TokenResponseDto>(okResult.Value);
        Assert.Equal(expectedToken.Token, tokenResponse.Token);
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ReturnsUnauthorized()
    {
        var dto = new LoginDto { Email = "nonexistent@example.com", Password = "Password123!" };
        _userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((AppUser)null);

        var result = await _controller.Login(dto);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
        Assert.Equal("Invalid credentials or email not confirmed", unauthorizedResult.Value);
    }

    [Fact]
    public async Task Login_WithUnconfirmedEmail_ReturnsUnauthorized()
    {
        var dto = new LoginDto { Email = "test@example.com", Password = "Password123!" };
        var user = new AppUser
        {
            Id = "user-id",
            Email = "test@example.com",
            EmailConfirmed = false
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync("test@example.com"))
            .ReturnsAsync(user);

        var result = await _controller.Login(dto);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
        Assert.Equal("Invalid credentials or email not confirmed", unauthorizedResult.Value);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        var dto = new LoginDto { Email = "test@example.com", Password = "WrongPassword!" };
        var user = new AppUser
        {
            Id = "user-id",
            Email = "test@example.com",
            EmailConfirmed = true
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync("test@example.com"))
            .ReturnsAsync(user);
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, dto.Password, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        var result = await _controller.Login(dto);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
        Assert.Equal("Invalid credentials", unauthorizedResult.Value);
    }

    [Fact]
    public async Task Login_WithLockedOutUser_ReturnsUnauthorized()
    {
        var dto = new LoginDto { Email = "locked@example.com", Password = "Password123!" };
        var user = new AppUser
        {
            Id = "user-id",
            Email = "locked@example.com",
            EmailConfirmed = true
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync("locked@example.com"))
            .ReturnsAsync(user);
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, dto.Password, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.LockedOut);

        var result = await _controller.Login(dto);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
        Assert.Equal("Invalid credentials", unauthorizedResult.Value);
    }

    [Fact]
    public async Task Login_TrimsAndLowercasesEmail()
    {
        var dto = new LoginDto { Email = "  TEST@EXAMPLE.COM  ", Password = "Password123!" };
        var user = new AppUser
        {
            Id = "user-id",
            Email = "test@example.com",
            EmailConfirmed = true
        };
        var tokenResponse = new TokenResponseDto
        {
            Token = "token",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync("test@example.com"))
            .ReturnsAsync(user);
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, dto.Password, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _jwtServiceMock.Setup(x => x.GenerateTokenAsync(user))
            .ReturnsAsync(tokenResponse);

        await _controller.Login(dto);

        _userManagerMock.Verify(x => x.FindByEmailAsync("test@example.com"), Times.Once);
    }

    [Fact]
    public async Task Login_WithInvalidModel_ReturnsValidationProblem()
    {
        _controller.ModelState.AddModelError("Email", "The Email field is required.");
        var dto = new LoginDto { Email = null, Password = "Password123!" };

        var result = await _controller.Login(dto);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);

        _userManagerMock.Verify(x => x.FindByEmailAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Login_TokenGenerationFails_ReturnsInternalServerErrorWithGenericMessage()
    {
        var dto = new LoginDto { Email = "test@example.com", Password = "Password123!" };
        var user = new AppUser
        {
            Id = "user-id",
            Email = "test@example.com",
            EmailConfirmed = true
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync("test@example.com"))
            .ReturnsAsync(user);
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, dto.Password, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        _jwtServiceMock.Setup(x => x.GenerateTokenAsync(user))
            .ThrowsAsync(new System.Exception("JWT service internal error."));

        var result = await _controller.Login(dto);

        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);

        var errorMessage = statusCodeResult.Value?.ToString();
        Assert.DoesNotContain("JWT service internal error.", errorMessage);
        Assert.Contains("authentication", errorMessage, StringComparison.OrdinalIgnoreCase);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("JWT generation failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Login_DoesNotCallJwtServiceWhenCredentialsInvalid()
    {
        var dto = new LoginDto { Email = "test@example.com", Password = "WrongPassword!" };
        var user = new AppUser
        {
            Id = "user-id",
            Email = "test@example.com",
            EmailConfirmed = true
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync("test@example.com"))
            .ReturnsAsync(user);
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, dto.Password, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        await _controller.Login(dto);

        _jwtServiceMock.Verify(x => x.GenerateTokenAsync(It.IsAny<AppUser>()), Times.Never);
    }

    [Fact]
    public async Task Login_DoesNotCheckPasswordWhenEmailNotConfirmed()
    {
        var dto = new LoginDto { Email = "test@example.com", Password = "Password123!" };
        var user = new AppUser
        {
            Id = "user-id",
            Email = "test@example.com",
            EmailConfirmed = false
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync("test@example.com"))
            .ReturnsAsync(user);

        await _controller.Login(dto);

        _signInManagerMock.Verify(
            x => x.CheckPasswordSignInAsync(It.IsAny<AppUser>(), It.IsAny<string>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task Login_WithNullEmail_ReturnsUnauthorized()
    {
        var dto = new LoginDto { Email = null, Password = "Password123!" };

        var result = await _controller.Login(dto);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
        Assert.Equal("Invalid credentials", unauthorizedResult.Value);

        _userManagerMock.Verify(x => x.FindByEmailAsync(It.IsAny<string>()), Times.Never);
    }
}