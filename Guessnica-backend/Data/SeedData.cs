using Guessnica_backend.Models;
using Microsoft.AspNetCore.Identity;

namespace Guessnica_backend.Data;

public static class SeedData
{
    public static async Task EnsureSeedDataAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var userManager = sp.GetRequiredService<UserManager<AppUser>>();
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var r in new[] { "User", "Admin" })
        {
            if (!await roleManager.RoleExistsAsync(r))
                await roleManager.CreateAsync(new IdentityRole(r));
        }

        await EnsureUserAsync(
            userManager,
            email: "test@example.com",
            displayName: "Test User",
            password: "Haslo123!",
            role: "User"
        );

        await EnsureUserAsync(
            userManager,
            email: "admin@example.com",
            displayName: "Admin User",
            password: "Admin123!",
            role: "Admin"
        );
    }

    private static async Task EnsureUserAsync(
        UserManager<AppUser> userManager,
        string email,
        string displayName,
        string password,
        string role)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user == null)
        {
            user = new AppUser
            {
                UserName = email,
                Email = email,
                DisplayName = displayName,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new Exception(string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}")));
        }

        if (!await userManager.IsInRoleAsync(user, role))
            await userManager.AddToRoleAsync(user, role);
    }
}