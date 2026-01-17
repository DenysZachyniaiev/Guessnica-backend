using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication;
using Guessnica_backend.Models;
using Guessnica_backend.Services;

namespace Guessnica_backend.Tests.Controllers;

public class ConfirmEmailTests
{
    private readonly Mock<UserManager<AppUser>> _userManagerMock;
    private readonly Mock<SignInManager<AppUser>> _signInManagerMock;
    private readonly Mock<IJwtService> _jwtServiceMock;
    private readonly Mock<ILogger<AuthController>> _loggerMock;
    private readonly AuthController _controller;

    public ConfirmEmailTests()
    {
        var userStoreMock = new Mock<IUserStore<AppUser>>();
        var optionsMock = new Mock<IOptions<IdentityOptions>>();
        var passwordHasherMock = new Mock<IPasswordHasher<AppUser>>();
        var userValidators = new List<IUserValidator<AppUser>>();
        var passwordValidators = new List<IPasswordValidator<AppUser>>();
        var keyNormalizerMock = new Mock<ILookupNormalizer>();
        var errorsMock = new Mock<IdentityErrorDescriber>();
        var servicesMock = new Mock<IServiceProvider>();
        var loggerUserManagerMock = new Mock<ILogger<UserManager<AppUser>>>();

        _userManagerMock = new Mock<UserManager<AppUser>>(
            userStoreMock.Object,
            optionsMock.Object,
            passwordHasherMock.Object,
            userValidators,
            passwordValidators,
            keyNormalizerMock.Object,
            errorsMock.Object,
            servicesMock.Object,
            loggerUserManagerMock.Object);

        var contextAccessorMock = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var claimsFactoryMock = new Mock<IUserClaimsPrincipalFactory<AppUser>>();
        var optionsSignInMock = new Mock<IOptions<IdentityOptions>>();
        var loggerSignInMock = new Mock<ILogger<SignInManager<AppUser>>>();
        var schemesMock = new Mock<IAuthenticationSchemeProvider>();
        var confirmationMock = new Mock<IUserConfirmation<AppUser>>();

        _signInManagerMock = new Mock<SignInManager<AppUser>>(
            _userManagerMock.Object,
            contextAccessorMock.Object,
            claimsFactoryMock.Object,
            optionsSignInMock.Object,
            loggerSignInMock.Object,
            schemesMock.Object,
            confirmationMock.Object);

        _jwtServiceMock = new Mock<IJwtService>();
        _loggerMock = new Mock<ILogger<AuthController>>();

        _controller = new AuthController(
            _userManagerMock.Object,
            _signInManagerMock.Object,
            _jwtServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ConfirmEmail_WithValidParameters_ReturnsOkWithSuccessMessage()
    {
        var userId = "user-id-123";
        var token = "valid-token";
        var user = new AppUser
        {
            Id = userId,
            Email = "test@example.com",
            EmailConfirmed = false
        };

        _userManagerMock.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _userManagerMock.Setup(x => x.ConfirmEmailAsync(user, token))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _controller.ConfirmEmail(userId, token);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        var value = okResult.Value;
        Assert.NotNull(value);
        var messageProperty = value.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        var message = messageProperty.GetValue(value) as string;
        Assert.Equal("Email confirmed successfully!", message);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Email confirmed successfully")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ConfirmEmail_WithNullUserId_ReturnsBadRequest()
    {
        string? userId = null;
        var token = "valid-token";

        var result = await _controller.ConfirmEmail(userId!, token);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);

        var value = badRequestResult.Value;
        Assert.NotNull(value);
        var messageProperty = value.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        var message = messageProperty.GetValue(value) as string;
        Assert.Equal("Invalid confirmation link.", message);

        _userManagerMock.Verify(x => x.FindByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmEmail_WithEmptyUserId_ReturnsBadRequest()
    {
        var userId = "";
        var token = "valid-token";

        var result = await _controller.ConfirmEmail(userId, token);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);

        var value = badRequestResult.Value;
        Assert.NotNull(value);
        var messageProperty = value.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        var message = messageProperty.GetValue(value) as string;
        Assert.Equal("Invalid confirmation link.", message);

        _userManagerMock.Verify(x => x.FindByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmEmail_WithNullToken_ReturnsBadRequest()
    {
        var userId = "user-id-123";
        string? token = null;

        var result = await _controller.ConfirmEmail(userId, token!);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);

        var value = badRequestResult.Value;
        Assert.NotNull(value);
        var messageProperty = value.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        var message = messageProperty.GetValue(value) as string;
        Assert.Equal("Invalid confirmation link.", message);

        _userManagerMock.Verify(x => x.FindByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmEmail_WithEmptyToken_ReturnsBadRequest()
    {
        var userId = "user-id-123";
        var token = "";

        var result = await _controller.ConfirmEmail(userId, token);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);

        var value = badRequestResult.Value;
        Assert.NotNull(value);
        var messageProperty = value.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        var message = messageProperty.GetValue(value) as string;
        Assert.Equal("Invalid confirmation link.", message);

        _userManagerMock.Verify(x => x.FindByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmEmail_WithNonExistentUser_ReturnsBadRequest()
    {
        var userId = "non-existent-id";
        var token = "valid-token";

        _userManagerMock.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync((AppUser?)null);

        var result = await _controller.ConfirmEmail(userId, token);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);

        var value = badRequestResult.Value;
        Assert.NotNull(value);
        var messageProperty = value.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        var message = messageProperty.GetValue(value) as string;
        Assert.Equal("Invalid confirmation link.", message);

        _userManagerMock.Verify(x => x.ConfirmEmailAsync(It.IsAny<AppUser>(), It.IsAny<string>()), Times.Never);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("user not found")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ConfirmEmail_WithInvalidToken_ReturnsBadRequest()
    {
        var userId = "user-id-123";
        var token = "invalid-token";
        var user = new AppUser
        {
            Id = userId,
            Email = "test@example.com",
            EmailConfirmed = false
        };

        var errors = new[]
        {
            new IdentityError { Code = "InvalidToken", Description = "Invalid token" }
        };

        _userManagerMock.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _userManagerMock.Setup(x => x.ConfirmEmailAsync(user, token))
            .ReturnsAsync(IdentityResult.Failed(errors));

        var result = await _controller.ConfirmEmail(userId, token);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
        var value = badRequestResult.Value;
        Assert.NotNull(value);
        var messageProperty = value.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        var message = messageProperty.GetValue(value) as string;
        Assert.Equal("Invalid confirmation link.", message);
    }

    [Fact]
    public async Task ConfirmEmail_CallsConfirmEmailAsync()
    {
        var userId = "user-id-123";
        var token = "valid-token";
        var user = new AppUser { Id = userId, Email = "test@example.com" };

        _userManagerMock.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _userManagerMock.Setup(x => x.ConfirmEmailAsync(user, token))
            .ReturnsAsync(IdentityResult.Success);

        await _controller.ConfirmEmail(userId, token);

        _userManagerMock.Verify(x => x.ConfirmEmailAsync(user, token), Times.Once);
    }

    [Fact]
    public async Task ConfirmEmail_UserAlreadyConfirmed_ReturnsOk()
    {
        var userId = "user-id-123";
        var token = "valid-token";
        var user = new AppUser
        {
            Id = userId,
            Email = "test@example.com",
            EmailConfirmed = true
        };

        _userManagerMock.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);
        _userManagerMock.Setup(x => x.ConfirmEmailAsync(user, token))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _controller.ConfirmEmail(userId, token);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        var value = okResult.Value;
        Assert.NotNull(value);
        var messageProperty = value.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        var message = messageProperty.GetValue(value) as string;
        Assert.Equal("Email confirmed successfully!", message);
        _userManagerMock.Verify(x => x.ConfirmEmailAsync(user, token), Times.Once);
    }

    [Fact]
    public async Task ConfirmEmail_WhenExceptionOccurs_ReturnsInternalServerError()
    {
        var userId = "user-id-123";
        var token = "valid-token";

        _userManagerMock.Setup(x => x.FindByIdAsync(userId))
            .ThrowsAsync(new System.Exception("Database connection failed"));

        var result = await _controller.ConfirmEmail(userId, token);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);

        var value = objectResult.Value;
        Assert.NotNull(value);
        var messageProperty = value.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        var message = messageProperty.GetValue(value) as string;
        Assert.Contains("error occurred during email confirmation", message, StringComparison.OrdinalIgnoreCase);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unexpected error")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}