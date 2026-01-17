namespace Guessnica_backend.Dtos;

using System.ComponentModel.DataAnnotations;

public class RequestPasswordResetDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}