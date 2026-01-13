namespace Guessnica_backend.Dtos;

using System;
using System.ComponentModel.DataAnnotations;

public record SetNewPasswordDto(
    [Required, EmailAddress] string Email,
    [Required] Guid ResetSessionId,
    [Required] string NewPassword
);