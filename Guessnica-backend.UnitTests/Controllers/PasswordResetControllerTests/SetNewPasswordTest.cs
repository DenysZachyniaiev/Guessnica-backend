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
using Guessnica_backend.Dtos;
using Guessnica_backend.Data;

namespace Guessnica_backend.Tests.Controllers;

public class SetNewPasswordTests
{
    private readonly Mock<UserManager<AppUser>> _userManagerMock;
    private readonly Mock<IAppEmailSender> _emailSenderMock;
    private readonly AppDbContext _context;
    private readonly PasswordResetController _controller;

    public SetNewPasswordTests()
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
public async Task SetNewPassword_WithValidSession_ResetsPasswordSuccessfully()
{
    var user = new AppUser
    {
        Id = "user-id",
        Email = "user@example.com",
        UserName = "user@example.com"
    };

    var resetSessionId = Guid.NewGuid();
    var resetToken = "valid-reset-token";
    var codeRecord = new UserVerificationCode
    {
        Id = Guid.NewGuid(),
        UserId = user.Id,
        Purpose = "password_reset",
        CodeHash = "hash",
        ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
        CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
        UsedAtUtc = DateTime.UtcNow.AddMinutes(-3),
        ResetSessionId = resetSessionId,
        ResetSessionExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
        IdentityResetToken = resetToken
    };
    _context.UserVerificationCodes.Add(codeRecord);
    await _context.SaveChangesAsync();

    var dto = new SetNewPasswordDto(
        Email: "user@example.com",
        ResetSessionId: resetSessionId,
        NewPassword: "NewPassword123!"
    );

    _userManagerMock.Setup(x => x.FindByEmailAsync("user@example.com"))
        .ReturnsAsync(user);
    _userManagerMock.Setup(x => x.ResetPasswordAsync(user, resetToken, dto.NewPassword))
        .ReturnsAsync(IdentityResult.Success);
    _userManagerMock.Setup(x => x.UpdateSecurityStampAsync(user))
        .ReturnsAsync(IdentityResult.Success);

    var result = await _controller.SetNewPassword(dto);

    var okResult = Assert.IsType<OkObjectResult>(result);
    var value = okResult.Value;
    Assert.NotNull(value);
    
    var messageProp = value.GetType().GetProperty("message");
    Assert.NotNull(messageProp);
    
    var message = messageProp.GetValue(value) as string;
    Assert.Equal("Password reset successfully.", message);

    _userManagerMock.Verify(x => x.ResetPasswordAsync(user, resetToken, dto.NewPassword), Times.Once);
    _userManagerMock.Verify(x => x.UpdateSecurityStampAsync(user), Times.Once);
}

    [Fact]
    public async Task SetNewPassword_WithNonExistentUser_ReturnsUnauthorized()
    {
        var dto = new SetNewPasswordDto(
            Email: "nonexistent@example.com",
            ResetSessionId: Guid.NewGuid(),
            NewPassword: "NewPassword123!"
        );

        _userManagerMock.Setup(x => x.FindByEmailAsync("nonexistent@example.com"))
            .ReturnsAsync((AppUser?)null);

        var result = await _controller.SetNewPassword(dto);


        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Invalid session", unauthorizedResult.Value);
    }

    [Fact]
    public async Task SetNewPassword_WithInvalidSessionId_ReturnsUnauthorized()
    {
        var user = new AppUser
        {
            Id = "user-id",
            Email = "user@example.com",
            UserName = "user@example.com"
        };

        var dto = new SetNewPasswordDto(
            Email: "user@example.com",
            ResetSessionId: Guid.NewGuid(),
            NewPassword: "NewPassword123!"
        );

        _userManagerMock.Setup(x => x.FindByEmailAsync("user@example.com"))
            .ReturnsAsync(user);


        var result = await _controller.SetNewPassword(dto);


        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Invalid session", unauthorizedResult.Value);
    }

    [Fact]
    public async Task SetNewPassword_WithExpiredSession_ReturnsUnauthorized()
    {
        var user = new AppUser
        {
            Id = "user-id",
            Email = "user@example.com",
            UserName = "user@example.com"
        };

        var resetSessionId = Guid.NewGuid();
        var codeRecord = new UserVerificationCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Purpose = "password_reset",
            CodeHash = "hash",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-20),
            UsedAtUtc = DateTime.UtcNow.AddMinutes(-18),
            ResetSessionId = resetSessionId,
            ResetSessionExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1), // Expired
            IdentityResetToken = "token"
        };
        _context.UserVerificationCodes.Add(codeRecord);
        await _context.SaveChangesAsync();

        var dto = new SetNewPasswordDto(
            Email: "user@example.com",
            ResetSessionId: resetSessionId,
            NewPassword: "NewPassword123!"
        );

        _userManagerMock.Setup(x => x.FindByEmailAsync("user@example.com"))
            .ReturnsAsync(user);

        var result = await _controller.SetNewPassword(dto);


        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Invalid session", unauthorizedResult.Value);
    }

    [Fact]
    public async Task SetNewPassword_WithPasswordResetFailure_ReturnsBadRequest()
    {
        var user = new AppUser
        {
            Id = "user-id",
            Email = "user@example.com",
            UserName = "user@example.com"
        };

        var resetSessionId = Guid.NewGuid();
        var resetToken = "valid-reset-token";
        var codeRecord = new UserVerificationCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Purpose = "password_reset",
            CodeHash = "hash",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            UsedAtUtc = DateTime.UtcNow.AddMinutes(-3),
            ResetSessionId = resetSessionId,
            ResetSessionExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
            IdentityResetToken = resetToken
        };
        _context.UserVerificationCodes.Add(codeRecord);
        await _context.SaveChangesAsync();

        var dto = new SetNewPasswordDto(
            Email: "user@example.com",
            ResetSessionId: resetSessionId,
            NewPassword: "weak"
        );

        var errors = new[]
        {
            new IdentityError { Code = "PasswordTooShort", Description = "Password is too short" }
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync("user@example.com"))
            .ReturnsAsync(user);
        _userManagerMock.Setup(x => x.ResetPasswordAsync(user, resetToken, dto.NewPassword))
            .ReturnsAsync(IdentityResult.Failed(errors));

        var result = await _controller.SetNewPassword(dto);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var errorMessage = badRequestResult.Value as string;
        Assert.NotNull(errorMessage);
        Assert.Contains("Password is too short", errorMessage);
    }

    [Fact]
    public async Task SetNewPassword_ExpiresResetSession()
    {
        var user = new AppUser
        {
            Id = "user-id",
            Email = "user@example.com",
            UserName = "user@example.com"
        };

        var resetSessionId = Guid.NewGuid();
        var resetToken = "valid-reset-token";
        var codeId = Guid.NewGuid();
        var codeRecord = new UserVerificationCode
        {
            Id = codeId,
            UserId = user.Id,
            Purpose = "password_reset",
            CodeHash = "hash",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            UsedAtUtc = DateTime.UtcNow.AddMinutes(-3),
            ResetSessionId = resetSessionId,
            ResetSessionExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
            IdentityResetToken = resetToken
        };
        _context.UserVerificationCodes.Add(codeRecord);
        await _context.SaveChangesAsync();

        var dto = new SetNewPasswordDto(
            Email: "user@example.com",
            ResetSessionId: resetSessionId,
            NewPassword: "NewPassword123!"
        );

        _userManagerMock.Setup(x => x.FindByEmailAsync("user@example.com"))
            .ReturnsAsync(user);
        _userManagerMock.Setup(x => x.ResetPasswordAsync(user, resetToken, dto.NewPassword))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(x => x.UpdateSecurityStampAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        var beforeReset = DateTime.UtcNow;

        await _controller.SetNewPassword(dto);

        var afterReset = DateTime.UtcNow;

        var updatedRecord = await _context.UserVerificationCodes.FindAsync(codeId);
        Assert.NotNull(updatedRecord);
        Assert.NotNull(updatedRecord.ResetSessionExpiresAtUtc);
        Assert.True(updatedRecord.ResetSessionExpiresAtUtc <= afterReset);
        Assert.True(updatedRecord.ResetSessionExpiresAtUtc >= beforeReset);
    }

    [Fact]
    public async Task SetNewPassword_UpdatesSecurityStamp()
    {
        var user = new AppUser
        {
            Id = "user-id",
            Email = "user@example.com",
            UserName = "user@example.com"
        };

        var resetSessionId = Guid.NewGuid();
        var resetToken = "valid-reset-token";
        var codeRecord = new UserVerificationCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Purpose = "password_reset",
            CodeHash = "hash",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            UsedAtUtc = DateTime.UtcNow.AddMinutes(-3),
            ResetSessionId = resetSessionId,
            ResetSessionExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
            IdentityResetToken = resetToken
        };
        _context.UserVerificationCodes.Add(codeRecord);
        await _context.SaveChangesAsync();

        var dto = new SetNewPasswordDto(
            Email: "user@example.com",
            ResetSessionId: resetSessionId,
            NewPassword: "NewPassword123!"
        );

        _userManagerMock.Setup(x => x.FindByEmailAsync("user@example.com"))
            .ReturnsAsync(user);
        _userManagerMock.Setup(x => x.ResetPasswordAsync(user, resetToken, dto.NewPassword))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(x => x.UpdateSecurityStampAsync(user))
            .ReturnsAsync(IdentityResult.Success);


        await _controller.SetNewPassword(dto);


        _userManagerMock.Verify(x => x.UpdateSecurityStampAsync(user), Times.Once);
    }

    [Fact]
    public async Task SetNewPassword_TrimsAndLowercasesEmail()
    {
        var user = new AppUser
        {
            Id = "user-id",
            Email = "user@example.com",
            UserName = "user@example.com"
        };

        var dto = new SetNewPasswordDto(
            Email: "  USER@EXAMPLE.COM  ",
            ResetSessionId: Guid.NewGuid(),
            NewPassword: "NewPassword123!"
        );

        _userManagerMock.Setup(x => x.FindByEmailAsync("user@example.com"))
            .ReturnsAsync(user);

        await _controller.SetNewPassword(dto);
        
        _userManagerMock.Verify(x => x.FindByEmailAsync("user@example.com"), Times.Once);
    }
}