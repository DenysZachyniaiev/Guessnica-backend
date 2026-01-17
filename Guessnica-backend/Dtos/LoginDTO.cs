using System.ComponentModel.DataAnnotations;

namespace Guessnica_backend.Dtos;

public class LoginDto
{
    /// <example>test@example.com</example>
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <example>Password123!</example>
    [Required]
    public string Password { get; set; } = string.Empty;
}