using Guessnica_backend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Guessnica_backend.Data;

public class AppDbContext : IdentityDbContext<AppUser, IdentityRole, string>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserVerificationCode> UserVerificationCodes => Set<UserVerificationCode>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        
        b.Entity<UserVerificationCode>()
            .HasIndex(x => new { x.UserId, x.Purpose, x.ExpiresAtUtc });
        
        b.Entity<UserVerificationCode>()
            .HasIndex(x => x.ResetSessionId);
    }
}