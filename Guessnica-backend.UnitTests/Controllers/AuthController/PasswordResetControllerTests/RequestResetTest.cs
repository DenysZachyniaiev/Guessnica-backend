using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Guessnica_backend.Controllers;
using Guessnica_backend.Models;
using Guessnica_backend.Services;
using Guessnica_backend.Dtos;
using Guessnica_backend.Data;

namespace Guessnica_backend.Tests.Controllers;

public class RequestResetTests
{
    private readonly Mock<UserManager<AppUser>> _userManagerMock;
    private readonly Mock<IAppEmailSender> _emailSenderMock;
    private readonly AppDbContext _context;
    private readonly PasswordResetController _controller;

    public RequestResetTests()
    {
        var userStoreMock = new Mock<IUserStore<AppUser>>();
        _userManagerMock = new Mock<UserManager<AppUser>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);

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
    public async Task RequestReset_WithValidEmail_CreatesCodeAndSendsEmail()
    {
        var dto = new RequestPasswordResetDto { Email = "user@example.com" };
        var user = new AppUser
        {
            Id = "user-id",
            Email = "user@example.com",
            UserName = "user@example.com"
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync("user@example.com"))
            .ReturnsAsync(user);

  
        var result = await _controller.RequestReset(dto);

    
        var okResult = Assert.IsType<OkObjectResult>(result);
        
        var codeRecord = await _context.UserVerificationCodes
            .FirstOrDefaultAsync(x => x.UserId == user.Id && x.Purpose == "password_reset");
        
        Assert.NotNull(codeRecord);
        Assert.Null(codeRecord.UsedAtUtc);
        Assert.True(codeRecord.ExpiresAtUtc > DateTime.UtcNow);
        
        _emailSenderMock.Verify(x => x.SendAsync(
            "user@example.com",
            "Guessnica — Password Reset",
            It.Is<string>(s => s.Contains("Your password reset code:")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestReset_WithNonExistentEmail_ReturnsOkWithoutSendingEmail()
    {
       
        var dto = new RequestPasswordResetDto { Email = "nonexistent@example.com" };

        _userManagerMock.Setup(x => x.FindByEmailAsync("nonexistent@example.com"))
            .ReturnsAsync((AppUser)null);


        var result = await _controller.RequestReset(dto);

   
        var okResult = Assert.IsType<OkObjectResult>(result);
        _emailSenderMock.Verify(x => x.SendAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequestReset_RemovesOldUnusedCodes()
    {
        
        var dto = new RequestPasswordResetDto { Email = "user@example.com" };
        var user = new AppUser
        {
            Id = "user-id",
            Email = "user@example.com",
            UserName = "user@example.com"
        };
        
        var oldCode = new UserVerificationCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Purpose = "password_reset",
            CodeHash = "old-hash",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10),
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
        };
        _context.UserVerificationCodes.Add(oldCode);
        await _context.SaveChangesAsync();

        _userManagerMock.Setup(x => x.FindByEmailAsync("user@example.com"))
            .ReturnsAsync(user);

   
        await _controller.RequestReset(dto);
        
        var codes = await _context.UserVerificationCodes
            .Where(x => x.UserId == user.Id && x.Purpose == "password_reset")
            .ToListAsync();
        
        Assert.Single(codes);
        Assert.NotEqual(oldCode.Id, codes[0].Id);
    }

    [Fact]
    public async Task RequestReset_TrimsAndLowercasesEmail()
    {
       
        var dto = new RequestPasswordResetDto { Email = "  USER@EXAMPLE.COM  " };
        var user = new AppUser
        {
            Id = "user-id",
            Email = "user@example.com",
            UserName = "user@example.com"
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync("user@example.com"))
            .ReturnsAsync(user);
        
        await _controller.RequestReset(dto);
        
        _userManagerMock.Verify(x => x.FindByEmailAsync("user@example.com"), Times.Once);
    }

    [Fact]
    public async Task RequestReset_CodeExpiresIn15Minutes()
    {
        var dto = new RequestPasswordResetDto { Email = "user@example.com" };
        var user = new AppUser
        {
            Id = "user-id",
            Email = "user@example.com",
            UserName = "user@example.com"
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync("user@example.com"))
            .ReturnsAsync(user);

        var beforeRequest = DateTime.UtcNow;
        
        await _controller.RequestReset(dto);

        var afterRequest = DateTime.UtcNow;
        
        var codeRecord = await _context.UserVerificationCodes
            .FirstOrDefaultAsync(x => x.UserId == user.Id && x.Purpose == "password_reset");
        
        Assert.NotNull(codeRecord);
        Assert.True(codeRecord.ExpiresAtUtc >= beforeRequest.AddMinutes(15));
        Assert.True(codeRecord.ExpiresAtUtc <= afterRequest.AddMinutes(15));
    }
    
}