using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Guessnica_backend.Services;
using Guessnica_backend.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Guessnica_backend.Tests.Services;

public class JwtServiceTests
{
    private readonly Mock<UserManager<AppUser>> _userManagerMock;
    private readonly IConfiguration _configuration;
    private readonly JwtService _service;

    public JwtServiceTests()
    {
        _userManagerMock = MockUserManager<AppUser>();
        _configuration = CreateConfiguration();
        _service = new JwtService(_userManagerMock.Object, _configuration);
    }
    private static IConfiguration CreateConfiguration()
    {
        var configValues = new Dictionary<string, string>
        {
            { "Jwt:Key", "ThisIsAVerySecretKeyForTestingPurposesWithAtLeast32Characters!" },
            { "Jwt:Issuer", "TestIssuer" },
            { "Jwt:Audience", "TestAudience" }
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configValues!)
            .Build();
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
    [Fact]
    public async Task JwtServiceTests_GenerateTokenAsync_ValidUser_ReturnsValidToken()
    {
        var user = new AppUser
        {
            Id = "user-123",
            Email = "test@example.com",
            UserName = "testuser"
        };

        var roles = new List<string> { "User" };
        var securityStamp = "test-stamp-123";

        _userManagerMock.Setup(um => um.GetRolesAsync(user))
            .ReturnsAsync(roles);
        _userManagerMock.Setup(um => um.GetSecurityStampAsync(user))
            .ReturnsAsync(securityStamp);

        var result = await _service.GenerateTokenAsync(user);

        result.Should().NotBeNull();
        result.Token.Should().NotBeNullOrEmpty();
        result.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(3), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task JwtServiceTests_GenerateTokenAsync_ValidUser_TokenContainsCorrectClaims()
    {
        var user = new AppUser
        {
            Id = "user-456",
            Email = "adam@example.com",
            UserName = "adam"
        };

        var roles = new List<string> { "User", "Admin" };
        var securityStamp = "stamp-456";

        _userManagerMock.Setup(um => um.GetRolesAsync(user))
            .ReturnsAsync(roles);
        _userManagerMock.Setup(um => um.GetSecurityStampAsync(user))
            .ReturnsAsync(securityStamp);

        var result = await _service.GenerateTokenAsync(user);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.Token);

        var subClaim = token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);
        subClaim.Should().NotBeNull();
        subClaim!.Value.Should().Be("user-456");

        var emailClaim = token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email);
        emailClaim.Should().NotBeNull();
        emailClaim!.Value.Should().Be("adam@example.com");

        var stampClaim = token.Claims.FirstOrDefault(c => c.Type == "sstamp");
        stampClaim.Should().NotBeNull();
        stampClaim!.Value.Should().Be("stamp-456");

        var roleClaims = token.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
        roleClaims.Should().HaveCount(2);
        roleClaims.Should().Contain("User");
        roleClaims.Should().Contain("Admin");
    }

    [Fact]
    public async Task JwtServiceTests_GenerateTokenAsync_UserWithNoEmail_UsesEmptyString()
    {
        var user = new AppUser
        {
            Id = "user-grabowsky",
            Email = null,
            UserName = "nomail"
        };

        _userManagerMock.Setup(um => um.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "User" });
        _userManagerMock.Setup(um => um.GetSecurityStampAsync(user))
            .ReturnsAsync("stamp-789");

        var result = await _service.GenerateTokenAsync(user);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.Token);

        var emailClaim = token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email);
        emailClaim.Should().NotBeNull();
        emailClaim!.Value.Should().Be("");
    }

    [Fact]
    public async Task JwtServiceTests_GenerateTokenAsync_UserWithNoRoles_GeneratesTokenWithoutRoleClaims()
    {
        var user = new AppUser
        {
            Id = "user-000",
            Email = "noroles@example.com",
            UserName = "noroles"
        };

        _userManagerMock.Setup(um => um.GetRolesAsync(user))
            .ReturnsAsync(new List<string>());
        _userManagerMock.Setup(um => um.GetSecurityStampAsync(user))
            .ReturnsAsync("stamp-000");

        var result = await _service.GenerateTokenAsync(user);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.Token);

        var roleClaims = token.Claims.Where(c => c.Type == ClaimTypes.Role).ToList();
        roleClaims.Should().BeEmpty();
    }

    [Fact]
    public async Task JwtServiceTests_GenerateTokenAsync_TokenHasCorrectIssuerAndAudience()
    {
        var user = new AppUser
        {
            Id = "user-111",
            Email = "test@example.com",
            UserName = "test"
        };

        _userManagerMock.Setup(um => um.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "User" });
        _userManagerMock.Setup(um => um.GetSecurityStampAsync(user))
            .ReturnsAsync("stamp-111");

        var result = await _service.GenerateTokenAsync(user);
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.Token);

        token.Issuer.Should().Be("TestIssuer");
        token.Audiences.Should().Contain("TestAudience");
    }

    [Fact]
    public async Task JwtServiceTests_GenerateTokenAsync_TokenExpiresIn3Hours()
    {
        var user = new AppUser
        {
            Id = "user-222",
            Email = "test@example.com",
            UserName = "test"
        };

        _userManagerMock.Setup(um => um.GetRolesAsync(user))
            .ReturnsAsync(new List<string>());
        _userManagerMock.Setup(um => um.GetSecurityStampAsync(user))
            .ReturnsAsync("stamp-222");

        var beforeGeneration = DateTime.UtcNow;
        var result = await _service.GenerateTokenAsync(user);

        var expectedExpiration = beforeGeneration.AddHours(3);
        result.ExpiresAt.Should().BeCloseTo(expectedExpiration, TimeSpan.FromSeconds(5));

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.Token);
        token.ValidTo.Should().BeCloseTo(expectedExpiration, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task JwtServiceTests_GenerateTokenAsync_MultipleRoles_AllRolesIncludedInToken()
    {
        var user = new AppUser
        {
            Id = "user-333",
            Email = "multirole@example.com",
            UserName = "multirole"
        };

        var roles = new List<string> { "User", "Admin", "Moderator", "VIP" };

        _userManagerMock.Setup(um => um.GetRolesAsync(user))
            .ReturnsAsync(roles);
        _userManagerMock.Setup(um => um.GetSecurityStampAsync(user))
            .ReturnsAsync("stamp-333");

        var result = await _service.GenerateTokenAsync(user);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.Token);

        var roleClaims = token.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
        roleClaims.Should().HaveCount(4);
        roleClaims.Should().BeEquivalentTo(roles);
    }

    [Fact]
    public async Task JwtServiceTests_GenerateTokenAsync_CallsUserManagerMethods()
    {
        var user = new AppUser
        {
            Id = "user-444",
            Email = "verify@example.com",
            UserName = "verify"
        };

        _userManagerMock.Setup(um => um.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "User" });
        _userManagerMock.Setup(um => um.GetSecurityStampAsync(user))
            .ReturnsAsync("stamp-444");

        await _service.GenerateTokenAsync(user);

        _userManagerMock.Verify(um => um.GetRolesAsync(user), Times.Once);
        _userManagerMock.Verify(um => um.GetSecurityStampAsync(user), Times.Once);
    }
    
}