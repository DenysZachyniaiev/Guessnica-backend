namespace Guessnica_backend.Dtos;

using System.ComponentModel.DataAnnotations;

public class FacebookLoginDto
{
    [Required]
    public string AccessToken { get; set; } = string.Empty;
}