using System.Net;
using System.Net.Http.Headers;
using Guessnica_backend.Dtos;
using Guessnica_backend.Models;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace Guessnica_backend.Integration.Test;

public class AuthControllerInitTests : IClassFixture<IntegrationTestGuessnicaFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationTestGuessnicaFactory _factory;

    public AuthControllerInitTests(IntegrationTestGuessnicaFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AuthControllerInitTest_InitLogin_WithValidCredentials_ReturnsToken()
    {
        var testUser = await CreateConfirmedTestUserAsync("test1@example.com", "Test123!");
        
        var loginRequest = new LoginDto
        {
            Email = "test1@example.com",
            Password = "Test123!"
        };

        var response = await _client.PostAsJsonAsync("/auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<TokenResponseDto>();
        Assert.NotNull(result);
        Assert.NotNull(result.Token);
        Assert.NotEmpty(result.Token);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task AuthControllerInitTest_InitLogin_WithInvalidPassword_ReturnsUnauthorized()
    {
        await CreateConfirmedTestUserAsync("test2@example.com", "Test123!");
        
        var loginRequest = new LoginDto
        {
            Email = "test2@example.com",
            Password = "WrongPassword123!"
        };

        var response = await _client.PostAsJsonAsync("/auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthControllerInitTest_InitLogin_WithNonExistentEmail_ReturnsUnauthorized()
    {
        var loginRequest = new LoginDto
        {
            Email = "nonexistent@example.com",
            Password = "Test123!"
        };

        var response = await _client.PostAsJsonAsync("/auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthControllerInitTest_LoginWithUnconfirmedEmail_ReturnsUnauthorized()
    {
        await CreateTestUserAsync("unconfirmed@example.com", "Test123!", emailConfirmed: false);

        var loginRequest = new LoginDto
        {
            Email = "unconfirmed@example.com",
            Password = "Test123!"
        };

        var response = await _client.PostAsJsonAsync("/auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid credentials or email not confirmed", content);
    }

    [Fact]
    public async Task AuthControllerInitTest_InitLogin_WithEmptyEmail_ReturnsBadRequest()
    {

        var loginRequest = new LoginDto
        {
            Email = "",
            Password = "Test123!"
        };

        var response = await _client.PostAsJsonAsync("/auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AuthControllerInitTest_InitLogin_WithCaseInsensitiveEmail_ReturnsToken()
    {
        await CreateConfirmedTestUserAsync("test3@example.com", "Test123!");
        
        var loginRequest = new LoginDto
        {
            Email = "TEST3@EXAMPLE.COM",
            Password = "Test123!"
        };

        var response = await _client.PostAsJsonAsync("/auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AuthControllerInitTest_InitRegister_WithValidData_ReturnsOk()
    {
        var registerRequest = new RegisterDto
        {
            Email = "newuser@example.com",
            Password = "NewUser123!",
            DisplayName = "New User"
        };

        var response = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("message"));
        Assert.Contains("instructions have been sent", result["message"]);
    }

    [Fact]
    public async Task AuthControllerInitTest_InitRegister_WithExistingEmail_ReturnsOkWithGenericMessage()
    {
        await CreateConfirmedTestUserAsync("existing@example.com", "Test123!");
        
        var registerRequest = new RegisterDto
        {
            Email = "existing@example.com",
            Password = "NewPassword123!",
            DisplayName = "Another User"
        };

        var response = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("message"));
        Assert.Contains("instructions have been sent", result["message"]);
    }

    [Fact]
    public async Task AuthControllerInitTest_InitRegister_WithInvalidEmail_ReturnsBadRequest()
    {
        var registerRequest = new RegisterDto
        {
            Email = "invalid-email",
            Password = "Test123!",
            DisplayName = "Test User"
        };

        var response = await _client.PostAsJsonAsync("/auth/register", registerRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AuthControllerInitTest_InitRegister_WithWeakPassword_ReturnsBadRequest()
    {
        var registerRequest = new RegisterDto
        {
            Email = "weakpass-init@example.com",
            Password = "weak",
            DisplayName = "Test User"
        };

        var response = await _client.PostAsJsonAsync("/auth/register", registerRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AuthControllerInitTest_InitRegister_CreatesUserWithUserRole()
    {
        var registerRequest = new RegisterDto
        {
            Email = "roletest@example.com",
            Password = "Test123!",
            DisplayName = "Role Test"
        };

        await _client.PostAsJsonAsync("/auth/register", registerRequest);

        var user = await GetUserByEmailAsync("roletest@example.com");
        Assert.NotNull(user);
        var roles = await GetUserRolesAsync(user);
        Assert.Contains("User", roles);
    }

    [Fact]
    public async Task AuthControllerInitTest_InitConfirmEmail_WithValidToken_ReturnsOkAndConfirmsEmail()
    {
        var user = await CreateTestUserAsync("confirm@example.com", "Test123!", emailConfirmed: false);
        var token = await GenerateEmailConfirmationTokenAsync(user);

        var response = await _client.GetAsync($"/auth/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(token)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(result);
        Assert.Contains("confirmed successfully", result["message"]);

        var updatedUser = await GetUserByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.True(updatedUser.EmailConfirmed);
    }

    [Fact]
    public async Task AuthControllerInitTest_InitConfirmEmail_WithInvalidToken_ReturnsBadRequest()
    {
        var user = await CreateTestUserAsync("invalid@example.com", "Test123!", emailConfirmed: false);

        var response = await _client.GetAsync($"/auth/confirm-email?userId={user.Id}&token=invalid-token");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AuthControllerInitTest_InitConfirmEmail_WithMissingUserId_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/auth/confirm-email?token=some-token");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AuthControllerInitTest_InitConfirmEmail_WithNonExistentUser_ReturnsBadRequest()
    {
        var response = await _client.GetAsync($"/auth/confirm-email?userId=non-existent-id&token=some-token");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AuthControllerInitTest_InitMe_WithValidToken_ReturnsUserInfo()
    {
        var user = await CreateConfirmedTestUserAsync("me@example.com", "Test123!");
        var token = await LoginAndGetTokenAsync("me@example.com", "Test123!");
        
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<MeResponseDto>();
        Assert.NotNull(result);
        Assert.Equal("me@example.com", result.Email);
        Assert.Equal(user.Id, result.Id);
        Assert.Contains("User", result.Roles);
    }

    [Fact]
    public async Task AuthControllerInitTest_InitMe_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthControllerInitTest_InitMe_WithInvalidToken_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");

        var response = await _client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthControllerInitTest_InitMe_WithExpiredToken_ReturnsUnauthorized()
    {
        var user = await CreateConfirmedTestUserAsync("expired-init@example.com", "Test123!");
        var token = await LoginAndGetTokenAsync("expired-init@example.com", "Test123!");

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var trackedUser = await userManager.FindByIdAsync(user.Id);
            if (trackedUser != null)
            {
                await userManager.UpdateSecurityStampAsync(trackedUser);
            }
        }
    
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthControllerInitTest_InitLogout_WithValidToken_ReturnsOk()
    {
        await CreateConfirmedTestUserAsync("logout@example.com", "Test123!");
        var token = await LoginAndGetTokenAsync("logout@example.com", "Test123!");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/auth/logout", new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(result);
        Assert.Contains("Logged out", result["message"]);
    }

    [Fact]
    public async Task AuthControllerInitTest_InitLogout_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.PostAsync("/auth/logout", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthControllerInitTest_InitFullAuthFlow_RegisterLoginConfirmLoginAgain_WorksCorrectly()
    {
        var registerRequest = new RegisterDto
        {
            Email = "fullflow@example.com",
            Password = "Test123!",
            DisplayName = "Full Flow User"
        };
        var registerResponse = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var loginRequest = new LoginDto
        {
            Email = "fullflow@example.com",
            Password = "Test123!"
        };
        var loginResponse1 = await _client.PostAsJsonAsync("/auth/login", loginRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse1.StatusCode);

        var user = await GetUserByEmailAsync("fullflow@example.com");
        Assert.NotNull(user);
        var token = await GenerateEmailConfirmationTokenAsync(user);
        var confirmResponse = await _client.GetAsync($"/auth/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(token)}");
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);

        var loginResponse2 = await _client.PostAsJsonAsync("/auth/login", loginRequest);
        Assert.Equal(HttpStatusCode.OK, loginResponse2.StatusCode);
        
        var loginResult = await loginResponse2.Content.ReadFromJsonAsync<TokenResponseDto>();
        Assert.NotNull(loginResult);
        Assert.NotEmpty(loginResult.Token);
        
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult.Token);
        var meResponse = await _client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        
        var logoutResponse = await _client.PostAsync("/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);
    }
    private async Task<AppUser> CreateTestUserAsync(string email, string password, bool emailConfirmed = true)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            DisplayName = email.Split('@')[0],
            EmailConfirmed = emailConfirmed
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new Exception($"Failed to create user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        await userManager.AddToRoleAsync(user, "User");
        
        return user;
    }

    private async Task<AppUser> CreateConfirmedTestUserAsync(string email, string password)
    {
        return await CreateTestUserAsync(email, password, emailConfirmed: true);
    }

    private async Task<AppUser?> GetUserByEmailAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        return await userManager.FindByEmailAsync(email);
    }

    private async Task<AppUser?> GetUserByIdAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        return await userManager.FindByIdAsync(userId);
    }

    private async Task<IList<string>> GetUserRolesAsync(AppUser user)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        return await userManager.GetRolesAsync(user);
    }

    private async Task<string> GenerateEmailConfirmationTokenAsync(AppUser user)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        return await userManager.GenerateEmailConfirmationTokenAsync(user);
    }

    private async Task UpdateSecurityStampAsync(AppUser user)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        await userManager.UpdateSecurityStampAsync(user);
    }

    private async Task<string> LoginAndGetTokenAsync(string email, string password)
    {
        var loginRequest = new LoginDto
        {
            Email = email,
            Password = password
        };

        var response = await _client.PostAsJsonAsync("/auth/login", loginRequest);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TokenResponseDto>();
        return result!.Token;
    }
}