namespace Guessnica_backend.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using Data;
using Models;
using Services;
using Services.Helpers;
using Dtos;

[ApiController]
[Route("auth/password")]
[Produces("application/json")]
public class PasswordResetController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly AppDbContext _db;
    private readonly IAppEmailSender _emailSender;

    public PasswordResetController(UserManager<AppUser> userManager,
        AppDbContext db,
        IAppEmailSender emailSender)
    {
        _userManager = userManager;
        _db = db;
        _emailSender = emailSender;
    }

    [AllowAnonymous]
    [HttpPost("request-reset")]
    public async Task<IActionResult> RequestReset([FromBody] RequestPasswordResetDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null) return Ok(new { message = "If the email exists, a code has been sent." });
        
        var now = DateTime.UtcNow;
        var old = await _db.UserVerificationCodes
            .Where(x => x.UserId == user.Id && x.Purpose == "password_reset" && x.UsedAtUtc == null && x.ExpiresAtUtc > now)
            .ToListAsync();
        _db.UserVerificationCodes.RemoveRange(old);
        
        var code = CodeGenerator.Generate6DigitCode();
        var entity = new UserVerificationCode
        {
            UserId = user.Id,
            Purpose = "password_reset",
            ExpiresAtUtc = now.AddMinutes(15)
        };
        entity.CodeHash = SecurityHelper.HashCode(code, entity.Id);
        _db.UserVerificationCodes.Add(entity);
        await _db.SaveChangesAsync();

        var body = $"Your password reset code: {code}\nIt is valid for 15 minutes.";
        await _emailSender.SendAsync(email, "Guessnica — Password Reset", body);

        return Ok(new { message = "If the email exists, a code has been sent." });
    }

    [AllowAnonymous]
    [HttpPost("verify-reset-code")]
    public async Task<IActionResult> VerifyResetCode([FromBody] VerifyResetCodeDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var user = await _userManager.FindByEmailAsync(dto.Email.Trim().ToLowerInvariant());
        if (user == null) return Unauthorized("Invalid code or expired");

        var now = DateTime.UtcNow;
        var candidate = await _db.UserVerificationCodes
            .Where(x => x.UserId == user.Id && x.Purpose == "password_reset" && x.UsedAtUtc == null && x.ExpiresAtUtc > now)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (candidate == null) return Unauthorized("Invalid code or expired");

        if (candidate.Attempts >= 5) return Unauthorized("Code locked due to too many attempts");

        candidate.Attempts++;
        if (!string.Equals(candidate.CodeHash, SecurityHelper.HashCode(dto.Code, candidate.Id), StringComparison.Ordinal))
        {
            await _db.SaveChangesAsync();
            return Unauthorized("Invalid code or expired");
        }

        candidate.UsedAtUtc = now;
        candidate.ResetSessionId = Guid.NewGuid();
        candidate.ResetSessionExpiresAtUtc = now.AddMinutes(15);
        candidate.IdentityResetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            resetSessionId = candidate.ResetSessionId,
            expiresAt = candidate.ResetSessionExpiresAtUtc
        });
    }

    [AllowAnonymous]
    [HttpPost("set-new-password")]
    public async Task<IActionResult> SetNewPassword([FromBody] SetNewPasswordDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var user = await _userManager.FindByEmailAsync(dto.Email.Trim().ToLowerInvariant());
        if (user == null) return Unauthorized("Invalid session");

        var now = DateTime.UtcNow;
        var record = await _db.UserVerificationCodes
            .Where(x => x.UserId == user.Id && x.Purpose == "password_reset" && x.ResetSessionId == dto.ResetSessionId
                        && x.ResetSessionExpiresAtUtc != null && x.ResetSessionExpiresAtUtc > now && x.IdentityResetToken != null)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (record == null) return Unauthorized("Invalid session");

        var res = await _userManager.ResetPasswordAsync(user, record.IdentityResetToken!, dto.NewPassword);
        if (!res.Succeeded)
            return BadRequest(string.Join("; ", res.Errors.Select(e => e.Description)));

        record.ResetSessionExpiresAtUtc = now;
        await _userManager.UpdateSecurityStampAsync(user);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Password reset successfully." });
    }
}
