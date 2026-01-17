using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Guessnica_backend.Controllers;
using Guessnica_backend.Models;
using Guessnica_backend.Services;
using Guessnica_backend.Services.Helpers;
using Guessnica_backend.Dtos;
using Guessnica_backend.Data;
using System.Collections.Generic;


namespace Guessnica_backend.Tests.Controllers;

public class VerifyResetCodeTests
{
    private readonly Mock<UserManager<AppUser>> _userManagerMock;
    private readonly Mock<IAppEmailSender> _emailSenderMock;
    private readonly AppDbContext _context;
    private readonly PasswordResetController _controller;

    public VerifyResetCodeTests()
    {
        var userStoreMock = new Mock<IUserStore<AppUser>>();
        var optionsAccessor = new Mock<IOptions<IdentityOptions>>();
        var passwordHasher = new Mock<IPasswordHasher<AppUser>>();
        var userValidators = new List<IUserValidator<AppUser>>();
        var passwordValidators = new List<IPasswordValidator<AppUser>>();
        var keyNormalizer = new Mock<ILookupNormalizer>();
        var errors = new Mock<IdentityErrorDescriber>();
        var services = new Mock<IServiceProvider>();
        var logger = new Mock<ILogger<UserManager<AppUser>>>();

        _userManagerMock = new Mock<UserManager<AppUser>>(
            userStoreMock.Object,
            optionsAccessor.Object,
            passwordHasher.Object,
            userValidators,
            passwordValidators,
            keyNormalizer.Object,
            errors.Object,
            services.Object,
            logger.Object);

        _emailSenderMock = new Mock<IAppEmailSender>();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);

        _controller = new PasswordResetController(
            _userManagerMock.Object,
            _context,
            _emailSenderMock.Object);
    }

    [Fact]
    public async Task VerifyResetCode_WithValidCode_ReturnsResetSessionId()
    {
        var user = new AppUser
        {
            Id = "user-id",
            Email = "user@example.com",
            UserName = "user@example.com"
        };

        var codeId = Guid.NewGuid();
        var code = "123456";
        var codeRecord = new UserVerificationCode
        {
            Id = codeId,
            UserId = user.Id,
            Purpose = "password_reset",
            CodeHash = SecurityHelper.HashCode(code, codeId),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
            CreatedAtUtc = DateTime.UtcNow,
            Attempts = 0
        };
        _context.UserVerificationCodes.Add(codeRecord);
        await _context.SaveChangesAsync();

        var dto = new VerifyResetCodeDto
        {
            Email = "user@example.com",
            Code = code
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync("user@example.com"))
            .ReturnsAsync(user);
        _userManagerMock.Setup(x => x.GeneratePasswordResetTokenAsync(user))
            .ReturnsAsync("reset-token");
        
        var result = await _controller.VerifyResetCode(dto);
        
        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = okResult.Value;
        Assert.NotNull(value);
        
        var resetSessionIdProp = value.GetType().GetProperty("resetSessionId");
        var expiresAtProp = value.GetType().GetProperty("expiresAt");
        
        Assert.NotNull(resetSessionIdProp);
        Assert.NotNull(expiresAtProp);
        
        var updatedRecord = await _context.UserVerificationCodes.FindAsync(codeId);
        Assert.NotNull(updatedRecord);
        Assert.NotNull(updatedRecord.UsedAtUtc);
        Assert.NotNull(updatedRecord.ResetSessionId);
        Assert.NotNull(updatedRecord.IdentityResetToken);
    }

    [Fact]
    public async Task VerifyResetCode_WithInvalidCode_ReturnsUnauthorized()
    {
        var user = new AppUser
        {
            Id = "user-id",
            Email = "user@example.com",
            UserName = "user@example.com"
        };

        var codeId = Guid.NewGuid();
        var correctCode = "123456";
        var codeRecord = new UserVerificationCode
        {
            Id = codeId,
            UserId = user.Id,
            Purpose = "password_reset",
            CodeHash = SecurityHelper.HashCode(correctCode, codeId),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
            CreatedAtUtc = DateTime.UtcNow,
            Attempts = 0
        };
        _context.UserVerificationCodes.Add(codeRecord);
        await _context.SaveChangesAsync();

        var dto = new VerifyResetCodeDto
        {
            Email = "user@example.com",
            Code = "wrong-code"
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync("user@example.com"))
            .ReturnsAsync(user);
        
        var result = await _controller.VerifyResetCode(dto);
        
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Invalid code or expired", unauthorizedResult.Value);
        
        var updatedRecord = await _context.UserVerificationCodes.FindAsync(codeId);
        Assert.NotNull(updatedRecord);
        Assert.Equal(1, updatedRecord.Attempts);
    }

    [Fact]
    public async Task VerifyResetCode_WithNonExistentUser_ReturnsUnauthorized()
    {
        var dto = new VerifyResetCodeDto
        {
            Email = "nonexistent@example.com",
            Code = "123456"
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync("nonexistent@example.com"))
            .ReturnsAsync((AppUser?)null);

        // Act
        var result = await _controller.VerifyResetCode(dto);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Invalid code or expired", unauthorizedResult.Value);
    }

    [Fact]
    public async Task VerifyResetCode_WithExpiredCode_ReturnsUnauthorized()
    {
        var user = new AppUser
        {
            Id = "user-id",
            Email = "user@example.com",
            UserName = "user@example.com"
        };

        var codeId = Guid.NewGuid();
        var code = "123456";
        var codeRecord = new UserVerificationCode
        {
            Id = codeId,
            UserId = user.Id,
            Purpose = "password_reset",
            CodeHash = SecurityHelper.HashCode(code, codeId),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1), // Expired
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-20),
            Attempts = 0
        };
        _context.UserVerificationCodes.Add(codeRecord);
        await _context.SaveChangesAsync();

        var dto = new VerifyResetCodeDto
        {
            Email = "user@example.com",
            Code = code
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync("user@example.com"))
            .ReturnsAsync(user);

        
        var result = await _controller.VerifyResetCode(dto);
        
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Invalid code or expired", unauthorizedResult.Value);
    }

    [Fact]
    public async Task VerifyResetCode_WithTooManyAttempts_ReturnsUnauthorized()
    {
        var user = new AppUser
        {
            Id = "user-id",
            Email = "user@example.com",
            UserName = "user@example.com"
        };

        var codeId = Guid.NewGuid();
        var code = "123456";
        var codeRecord = new UserVerificationCode
        {
            Id = codeId,
            UserId = user.Id,
            Purpose = "password_reset",
            CodeHash = SecurityHelper.HashCode(code, codeId),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
            CreatedAtUtc = DateTime.UtcNow,
            Attempts = 5
        };
        _context.UserVerificationCodes.Add(codeRecord);
        await _context.SaveChangesAsync();

        var dto = new VerifyResetCodeDto
        {
            Email = "user@example.com",
            Code = code
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync("user@example.com"))
            .ReturnsAsync(user);

        
        var result = await _controller.VerifyResetCode(dto);
        
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Code locked due to too many attempts", unauthorizedResult.Value);
    }

    [Fact]
    public async Task VerifyResetCode_IncrementsAttempts()
    {
        var user = new AppUser
        {
            Id = "user-id",
            Email = "user@example.com",
            UserName = "user@example.com"
        };

        var codeId = Guid.NewGuid();
        var code = "123456";
        var codeRecord = new UserVerificationCode
        {
            Id = codeId,
            UserId = user.Id,
            Purpose = "password_reset",
            CodeHash = SecurityHelper.HashCode(code, codeId),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
            CreatedAtUtc = DateTime.UtcNow,
            Attempts = 2
        };
        _context.UserVerificationCodes.Add(codeRecord);
        await _context.SaveChangesAsync();

        var dto = new VerifyResetCodeDto
        {
            Email = "user@example.com",
            Code = "wrong-code"
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync("user@example.com"))
            .ReturnsAsync(user);
        
        await _controller.VerifyResetCode(dto);
        
        var updatedRecord = await _context.UserVerificationCodes.FindAsync(codeId);
        Assert.NotNull(updatedRecord);
        Assert.Equal(3, updatedRecord.Attempts);
    }

    [Fact]
    public async Task VerifyResetCode_SetsResetSessionExpiry15Minutes()
    {
        var user = new AppUser
        {
            Id = "user-id",
            Email = "user@example.com",
            UserName = "user@example.com"
        };

        var codeId = Guid.NewGuid();
        var code = "123456";
        var codeRecord = new UserVerificationCode
        {
            Id = codeId,
            UserId = user.Id,
            Purpose = "password_reset",
            CodeHash = SecurityHelper.HashCode(code, codeId),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
            CreatedAtUtc = DateTime.UtcNow,
            Attempts = 0
        };
        _context.UserVerificationCodes.Add(codeRecord);
        await _context.SaveChangesAsync();

        var dto = new VerifyResetCodeDto
        {
            Email = "user@example.com",
            Code = code
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync("user@example.com"))
            .ReturnsAsync(user);
        _userManagerMock.Setup(x => x.GeneratePasswordResetTokenAsync(user))
            .ReturnsAsync("reset-token");

        var beforeVerify = DateTime.UtcNow;
        
        await _controller.VerifyResetCode(dto);

        var afterVerify = DateTime.UtcNow;
        
        var updatedRecord = await _context.UserVerificationCodes.FindAsync(codeId);
        Assert.NotNull(updatedRecord);
        Assert.NotNull(updatedRecord.ResetSessionExpiresAtUtc);
        Assert.True(updatedRecord.ResetSessionExpiresAtUtc >= beforeVerify.AddMinutes(15));
        Assert.True(updatedRecord.ResetSessionExpiresAtUtc <= afterVerify.AddMinutes(15));
    }
}