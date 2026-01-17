namespace Guessnica_backend.Dtos;

using System.ComponentModel.DataAnnotations;

public class VerifyResetCodeDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(6), MaxLength(6)]
    public string Code { get; set; } = string.Empty;
}