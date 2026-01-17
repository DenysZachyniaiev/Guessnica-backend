using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Guessnica_backend.Models;

public class AppUser : IdentityUser
{
    [MaxLength(50)]
    public string DisplayName { get; set; } = string.Empty;
}