using System.ComponentModel.DataAnnotations;

namespace Guessnica_backend.Services;

public class EmailOptions
{
    [Required] public string Host { get; set; } = "";
    [Range(1, 65535)] public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    [Required, EmailAddress] public string User { get; set; } = "";
    [Required] public string Password { get; set; } = "";
    public string FromName { get; set; } = "Guessnica";
    [Required, EmailAddress] public string FromEmail { get; set; } = "";
}