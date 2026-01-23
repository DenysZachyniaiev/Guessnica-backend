using Guessnica_backend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Guessnica_backend.Data;

public class AppDbContext : IdentityDbContext<AppUser, IdentityRole, string>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserVerificationCode> UserVerificationCodes => Set<UserVerificationCode>();
    
    public DbSet<UserRiddle> UserRiddles => Set<UserRiddle>();
    
    public DbSet<Location> Locations { get; set; }
    
    public DbSet<Riddle> Riddles { get; set; }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        
        b.Entity<UserVerificationCode>()
            .HasIndex(x => new { x.UserId, x.Purpose, x.ExpiresAtUtc });
        
        b.Entity<UserVerificationCode>()
            .HasIndex(x => x.ResetSessionId);
        
        b.Entity<UserRiddle>()
            .HasIndex(ur => new { ur.UserId, ur.AssignedAt });

        b.Entity<UserRiddle>()
            .HasOne(ur => ur.User)
            .WithMany()
            .HasForeignKey(ur => ur.UserId);

        b.Entity<UserRiddle>()
            .HasOne(ur => ur.Riddle)
            .WithMany()
            .HasForeignKey(ur => ur.RiddleId);
    }
}