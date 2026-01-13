using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Guessnica_backend.Models;
using Guessnica_backend.Services;
using Guessnica_backend.Dtos;
using System.Collections.Generic;

namespace Guessnica_backend.Tests.Controllers.AuthControllerTests;

public class FacebookLoginTests
{
    private readonly Mock<UserManager<AppUser>> _userManagerMock;
    private readonly Mock<SignInManager<AppUser>> _signInManagerMock;
    private readonly Mock<IJwtService> _jwtServiceMock;
    private readonly Mock<ILogger<AuthController>> _loggerMock;
    private readonly Mock<IFacebookAuthService> _facebookAuthServiceMock;
    private readonly AuthController _controller;

    public FacebookLoginTests()
    {
        var userStoreMock = new Mock<IUserStore<AppUser>>();
        var optionsAccessor = new Mock<IOptions<IdentityOptions>>();
        var passwordHasherMock = new Mock<IPasswordHasher<AppUser>>();
        var userValidatorsMock = new List<IUserValidator<AppUser>> { new Mock<IUserValidator<AppUser>>().Object };
        var passwordValidatorsMock = new List<IPasswordValidator<AppUser>> { new Mock<IPasswordValidator<AppUser>>().Object };
        var keyNormalizerMock = new Mock<ILookupNormalizer>();
        var errorsMock = new Mock<IdentityErrorDescriber>();
        var servicesMock = new Mock<IServiceProvider>();
        var userLoggerMock = new Mock<ILogger<UserManager<AppUser>>>();

        _userManagerMock = new Mock<UserManager<AppUser>>(
            userStoreMock.Object,
            optionsAccessor.Object,
            passwordHasherMock.Object,
            userValidatorsMock,
            passwordValidatorsMock,
            keyNormalizerMock.Object,
            errorsMock.Object,
            servicesMock.Object,
            userLoggerMock.Object);

        var contextAccessorMock = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var claimsFactoryMock = new Mock<IUserClaimsPrincipalFactory<AppUser>>();
        var optionsAccessorSignInMock = new Mock<Microsoft.Extensions.Options.IOptions<IdentityOptions>>();
        var signInLoggerMock = new Mock<ILogger<SignInManager<AppUser>>>();
        var schemesMock = new Mock<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>();
        var confirmationMock = new Mock<IUserConfirmation<AppUser>>();

        _signInManagerMock = new Mock<SignInManager<AppUser>>(
            _userManagerMock.Object,
            contextAccessorMock.Object,
            claimsFactoryMock.Object,
            optionsAccessorSignInMock.Object,
            signInLoggerMock.Object,
            schemesMock.Object,
            confirmationMock.Object);

        _jwtServiceMock = new Mock<IJwtService>();
        _loggerMock = new Mock<ILogger<AuthController>>();
        _facebookAuthServiceMock = new Mock<IFacebookAuthService>();

        _controller = new AuthController(
            _userManagerMock.Object,
            _signInManagerMock.Object,
            _jwtServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task FacebookLogin_WithValidToken_ReturnsOkWithJwtToken()
    {
        var dto = new FacebookLoginDto { AccessToken = "valid-facebook-token" };
        var expectedToken = new TokenResponseDto
        {
            Token = "jwt-token-string",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        _facebookAuthServiceMock
            .Setup(x => x.HandleFacebookLoginAsync(
                It.IsAny<FacebookLoginDto>(),
                It.IsAny<UserManager<AppUser>>(),
                It.IsAny<IJwtService>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedToken);

        var result = await _controller.FacebookLogin(dto, _facebookAuthServiceMock.Object);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        var tokenResponse = Assert.IsType<TokenResponseDto>(okResult.Value);
        Assert.Equal(expectedToken.Token, tokenResponse.Token);
    }

    [Fact]
    public async Task FacebookLogin_CallsFacebookAuthService()
    {
        var dto = new FacebookLoginDto { AccessToken = "facebook-token" };
        var tokenResponse = new TokenResponseDto
        {
            Token = "token",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        _facebookAuthServiceMock
            .Setup(x => x.HandleFacebookLoginAsync(
                It.IsAny<FacebookLoginDto>(),
                It.IsAny<UserManager<AppUser>>(),
                It.IsAny<IJwtService>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenResponse);

        await _controller.FacebookLogin(dto, _facebookAuthServiceMock.Object);

        _facebookAuthServiceMock.Verify(
            x => x.HandleFacebookLoginAsync(
                It.IsAny<FacebookLoginDto>(),
                It.IsAny<UserManager<AppUser>>(),
                It.IsAny<IJwtService>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FacebookLogin_PassesCorrectDto()
    {
        var specificToken = "specific-facebook-token";
        var dto = new FacebookLoginDto { AccessToken = specificToken };
        var tokenResponse = new TokenResponseDto
        {
            Token = "token",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        _facebookAuthServiceMock
            .Setup(x => x.HandleFacebookLoginAsync(
                It.Is<FacebookLoginDto>(d => d.AccessToken == specificToken),
                It.IsAny<UserManager<AppUser>>(),
                It.IsAny<IJwtService>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenResponse);

        var result = await _controller.FacebookLogin(dto, _facebookAuthServiceMock.Object);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task FacebookLogin_PassesUserManagerAndJwtService()
    {
        var dto = new FacebookLoginDto { AccessToken = "token" };
        var tokenResponse = new TokenResponseDto
        {
            Token = "jwt-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        UserManager<AppUser>? capturedUserManager = null;
        IJwtService? capturedJwtService = null;

        _facebookAuthServiceMock
            .Setup(x => x.HandleFacebookLoginAsync(
                It.IsAny<FacebookLoginDto>(),
                It.IsAny<UserManager<AppUser>>(),
                It.IsAny<IJwtService>(),
                It.IsAny<CancellationToken>()))
            .Callback<FacebookLoginDto, UserManager<AppUser>, IJwtService, CancellationToken>((d, um, jwt, ct) =>
            {
                capturedUserManager = um;
                capturedJwtService = jwt;
            })
            .ReturnsAsync(tokenResponse);

        await _controller.FacebookLogin(dto, _facebookAuthServiceMock.Object);

        Assert.Same(_userManagerMock.Object, capturedUserManager);
        Assert.Same(_jwtServiceMock.Object, capturedJwtService);
    }

    [Fact]
    public async Task FacebookLogin_WhenAuthServiceReturnsNullToken_ReturnsUnauthorized()
    {
        var dto = new FacebookLoginDto { AccessToken = "invalid-token" };

        _facebookAuthServiceMock
            .Setup(x => x.HandleFacebookLoginAsync(
                It.IsAny<FacebookLoginDto>(),
                It.IsAny<UserManager<AppUser>>(),
                It.IsAny<IJwtService>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TokenResponseDto?)null);

        var result = await _controller.FacebookLogin(dto, _facebookAuthServiceMock.Object);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);

        var value = unauthorizedResult.Value;
        var messageProperty = value?.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        var message = messageProperty.GetValue(value)?.ToString();
        Assert.Equal("Facebook authentication failed.", message);
    }

    [Fact]
    public async Task FacebookLogin_WithEmptyAccessToken_ValidatesModel()
    {
        _controller.ModelState.AddModelError("AccessToken", "Access token cannot be empty.");
        var dto = new FacebookLoginDto { AccessToken = "" };

        var result = await _controller.FacebookLogin(dto, _facebookAuthServiceMock.Object);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);

        var validationProblem = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        Assert.True(validationProblem.Errors.ContainsKey("AccessToken"));

        _facebookAuthServiceMock.Verify(x => x.HandleFacebookLoginAsync(
            It.IsAny<FacebookLoginDto>(),
            It.IsAny<UserManager<AppUser>>(),
            It.IsAny<IJwtService>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FacebookLogin_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        var dto = new FacebookLoginDto { AccessToken = "token" };

        _facebookAuthServiceMock
            .Setup(x => x.HandleFacebookLoginAsync(
                It.IsAny<FacebookLoginDto>(),
                It.IsAny<UserManager<AppUser>>(),
                It.IsAny<IJwtService>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Exception("Facebook API error"));

        var result = await _controller.FacebookLogin(dto, _facebookAuthServiceMock.Object);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);

        var message = objectResult.Value?.ToString();
        Assert.DoesNotContain("Facebook API error", message);
        Assert.Contains("authentication", message, StringComparison.OrdinalIgnoreCase);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Facebook authentication error")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task FacebookLogin_WithInvalidModel_ReturnsBadRequest()
    {
        _controller.ModelState.AddModelError("AccessToken", "Access token is required.");
        var dto = new FacebookLoginDto { AccessToken = string.Empty };

        var result = await _controller.FacebookLogin(dto, _facebookAuthServiceMock.Object);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);

        var validationProblem = Assert.IsType<ValidationProblemDetails>(badRequestResult.Value);
        Assert.True(validationProblem.Errors.ContainsKey("AccessToken"));

        _facebookAuthServiceMock.Verify(x => x.HandleFacebookLoginAsync(
            It.IsAny<FacebookLoginDto>(),
            It.IsAny<UserManager<AppUser>>(),
            It.IsAny<IJwtService>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}