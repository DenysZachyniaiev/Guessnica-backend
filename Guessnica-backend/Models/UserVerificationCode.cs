using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Guessnica_backend.Models;

public class UserVerificationCode
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = null!;

    [Required, MaxLength(128)]
    public string CodeHash { get; set; } = null!;

    [Required]
    public DateTime ExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UsedAtUtc { get; set; }

    public int Attempts { get; set; } = 0;

    [Required, MaxLength(40)]
    public string Purpose { get; set; } = null!;
    
    public Guid? ResetSessionId { get; set; }
    public DateTime? ResetSessionExpiresAtUtc { get; set; }
    
    public string? IdentityResetToken { get; set; }

    [ForeignKey(nameof(UserId))]
    public AppUser? User { get; set; }
}