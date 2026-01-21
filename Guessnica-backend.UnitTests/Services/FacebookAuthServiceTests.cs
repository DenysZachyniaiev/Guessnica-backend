using Xunit;
using Moq;
using Moq.Protected;
using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Identity;
using Guessnica_backend.Services;
using Guessnica_backend.Dtos;
using Guessnica_backend.Models;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Guessnica_backend.Tests.Services;

public class FacebookAuthServiceTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<UserManager<AppUser>> _userManagerMock;
    private readonly Mock<IJwtService> _jwtServiceMock;
    private readonly FacebookAuthService _service;

    public FacebookAuthServiceTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        
        _configurationMock = new Mock<IConfiguration>();
        _configurationMock.Setup(c => c["Authentication:Facebook:AppId"]).Returns("test-app-id");
        _configurationMock.Setup(c => c["Authentication:Facebook:AppSecret"]).Returns("test-app-secret");

        _userManagerMock = MockUserManager<AppUser>();
        _jwtServiceMock = new Mock<IJwtService>();

        _service = new FacebookAuthService(_httpClient, _configurationMock.Object);
    }
    private static Mock<UserManager<TUser>> MockUserManager<TUser>() where TUser : class
    {
        var store = new Mock<IUserStore<TUser>>();
        var optionsAccessor = new Mock<IOptions<IdentityOptions>>();
        var passwordHasher = new Mock<IPasswordHasher<TUser>>();
        var userValidators = new List<IUserValidator<TUser>>();
        var passwordValidators = new List<IPasswordValidator<TUser>>();
        var keyNormalizer = new Mock<ILookupNormalizer>();
        var errors = new Mock<IdentityErrorDescriber>();
        var services = new Mock<IServiceProvider>();
        var logger = new Mock<ILogger<UserManager<TUser>>>();
        
        var mock = new Mock<UserManager<TUser>>(
            store.Object,
            optionsAccessor.Object,
            passwordHasher.Object,
            userValidators,
            passwordValidators,
            keyNormalizer.Object,
            errors.Object,
            services.Object,
            logger.Object
        );
        return mock;
    }
    private void SetupHttpResponse<T>(T response)
    {
        var json = JsonSerializer.Serialize(response);
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(httpResponse);
    }

    private void SetupValidToken()
    {
        var response = new FacebookTokenDebugResponse(
            new FacebookTokenData(true, "test-app-id", "fb-123")
        );
        SetupHttpResponse(response);
    }

    private void SetupUserInfo(string id, string name, string? email)
    {
        var userInfo = new FacebookUserInfo(id, name, email);
        
        var json = JsonSerializer.Serialize(userInfo);
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    JsonSerializer.Serialize(new FacebookTokenDebugResponse(
                        new FacebookTokenData(true, "test-app-id", id)
                    )), 
                    System.Text.Encoding.UTF8, 
                    "application/json"
                )
            })
            .ReturnsAsync(httpResponse);
    }
    [Fact]
    public async Task FacebookAuthServiceTests_ValidateAccessTokenAsync_ValidToken_ReturnsTrue()
    {
        var accessToken = "valid-token";
        var response = new FacebookTokenDebugResponse(
            new FacebookTokenData(true, "test-app-id", "user-123")
        );

        SetupHttpResponse(response);

        var result = await _service.ValidateAccessTokenAsync(accessToken);

        result.ok.Should().BeTrue();
        result.userId.Should().Be("user-123");
    }

    [Fact]
    public async Task FacebookAuthServiceTests_ValidateAccessTokenAsync_InvalidToken_ReturnsFalse()
    {
        var accessToken = "invalid-token";
        var response = new FacebookTokenDebugResponse(
            new FacebookTokenData(false, "test-app-id", "user-123")
        );

        SetupHttpResponse(response);

        var result = await _service.ValidateAccessTokenAsync(accessToken);

        result.ok.Should().BeFalse();
    }

    [Fact]
    public async Task FacebookAuthServiceTests_ValidateAccessTokenAsync_WrongAppId_ReturnsFalse()
    {
        var accessToken = "token-for-different-app";
        var response = new FacebookTokenDebugResponse(
            new FacebookTokenData(true, "different-app-id", "user-123")
        );

        SetupHttpResponse(response);
        
        var result = await _service.ValidateAccessTokenAsync(accessToken);

        result.ok.Should().BeFalse();
    }

    [Fact]
    public async Task FacebookAuthServiceTests_GetUserInfoAsync_ValidToken_ReturnsUserInfo()
    {
        var accessToken = "valid-token";
        var userInfo = new FacebookUserInfo("123", "Adam Grabowsky", "grabowsky@example.com");

        SetupHttpResponse(userInfo);

        var result = await _service.GetUserInfoAsync(accessToken);

        result.Should().NotBeNull();
        result!.Id.Should().Be("123");
        result.Name.Should().Be("Adam Grabowsky");
        result.Email.Should().Be("grabowsky@example.com");
    }

    [Fact]
    public async Task FacebookAuthServiceTests_HandleFacebookLoginAsync_NewUser_CreatesUserAndReturnsToken()
    {
        var dto = new FacebookLoginDto { AccessToken = "valid-token" };
        var tokenResponse = new TokenResponseDto 
        { 
            Token = "jwt-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        SetupValidToken();
        SetupUserInfo("fb-123", "Adam Grabowsky", "grabowsky@example.com");

        _userManagerMock.Setup(um => um.FindByLoginAsync("Facebook", "fb-123"))
            .ReturnsAsync((AppUser?)null);
        _userManagerMock.Setup(um => um.FindByEmailAsync("grabowsky@example.com"))
            .ReturnsAsync((AppUser?)null);
        _userManagerMock.Setup(um => um.CreateAsync(It.IsAny<AppUser>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(um => um.AddToRoleAsync(It.IsAny<AppUser>(), "User"))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(um => um.GetLoginsAsync(It.IsAny<AppUser>()))
            .ReturnsAsync(new List<UserLoginInfo>());
        _userManagerMock.Setup(um => um.AddLoginAsync(It.IsAny<AppUser>(), It.IsAny<UserLoginInfo>()))
            .ReturnsAsync(IdentityResult.Success);

        _jwtServiceMock.Setup(j => j.GenerateTokenAsync(It.IsAny<AppUser>()))
            .ReturnsAsync(tokenResponse);

        var result = await _service.HandleFacebookLoginAsync(dto, _userManagerMock.Object, _jwtServiceMock.Object);

        result.Should().NotBeNull();
        result.Token.Should().Be("jwt-token");
        _userManagerMock.Verify(um => um.CreateAsync(It.Is<AppUser>(u => 
            u.Email == "grabowsky@example.com" && 
            u.DisplayName == "Adam Grabowsky" &&
            u.EmailConfirmed == true
        )), Times.Once);
    }

    [Fact]
    public async Task FacebookAuthServiceTests_HandleFacebookLoginAsync_ExistingUser_ReturnsToken()
    {
        var dto = new FacebookLoginDto { AccessToken = "valid-token" };
        var existingUser = new AppUser 
        { 
            Id = "user-1", 
            Email = "grabowsky@example.com",
            EmailConfirmed = true
        };
        var tokenResponse = new TokenResponseDto 
        { 
            Token = "jwt-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        SetupValidToken();
        SetupUserInfo("fb-123", "Adam Grabowsky", "grabowsky@example.com");

        _userManagerMock.Setup(um => um.FindByLoginAsync("Facebook", "fb-123"))
            .ReturnsAsync(existingUser);
        _userManagerMock.Setup(um => um.GetLoginsAsync(existingUser))
            .ReturnsAsync(new List<UserLoginInfo> 
            { 
                new UserLoginInfo("Facebook", "fb-123", "Facebook") 
            });

        _jwtServiceMock.Setup(j => j.GenerateTokenAsync(existingUser))
            .ReturnsAsync(tokenResponse);

        var result = await _service.HandleFacebookLoginAsync(dto, _userManagerMock.Object, _jwtServiceMock.Object);

        result.Should().NotBeNull();
        result.Token.Should().Be("jwt-token");
        _userManagerMock.Verify(um => um.CreateAsync(It.IsAny<AppUser>()), Times.Never);
    }

    [Fact]
    public async Task FacebookAuthServiceTests_HandleFacebookLoginAsync_InvalidToken_ThrowsUnauthorizedAccessException()
    {
        var dto = new FacebookLoginDto { AccessToken = "invalid-token" };
        var response = new FacebookTokenDebugResponse(
            new FacebookTokenData(false, "test-app-id", "user-123")
        );

        SetupHttpResponse(response);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.HandleFacebookLoginAsync(dto, _userManagerMock.Object, _jwtServiceMock.Object)
        );
    }

    [Fact]
    public async Task FacebookAuthServiceTests_HandleFacebookLoginAsync_UserWithoutEmail_CreatesUserWithFallbackEmail()
    {
        var dto = new FacebookLoginDto { AccessToken = "valid-token" };
        var tokenResponse = new TokenResponseDto 
        { 
            Token = "jwt-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        SetupValidToken();
        SetupUserInfo("fb-123", "Adam Grabowsky", null);

        _userManagerMock.Setup(um => um.FindByLoginAsync("Facebook", "fb-123"))
            .ReturnsAsync((AppUser?)null);
        _userManagerMock.Setup(um => um.CreateAsync(It.IsAny<AppUser>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(um => um.AddToRoleAsync(It.IsAny<AppUser>(), "User"))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(um => um.GetLoginsAsync(It.IsAny<AppUser>()))
            .ReturnsAsync(new List<UserLoginInfo>());
        _userManagerMock.Setup(um => um.AddLoginAsync(It.IsAny<AppUser>(), It.IsAny<UserLoginInfo>()))
            .ReturnsAsync(IdentityResult.Success);

        _jwtServiceMock.Setup(j => j.GenerateTokenAsync(It.IsAny<AppUser>()))
            .ReturnsAsync(tokenResponse);

        var result = await _service.HandleFacebookLoginAsync(dto, _userManagerMock.Object, _jwtServiceMock.Object);

        _userManagerMock.Verify(um => um.CreateAsync(It.Is<AppUser>(u =>
            u.UserName != null &&
            u.UserName.StartsWith("fb_") &&
            u.UserName.EndsWith("@no-email.facebook.local")
        )), Times.Once);

    }
}