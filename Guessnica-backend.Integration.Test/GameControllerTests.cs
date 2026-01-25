using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Guessnica_backend.Data;
using Guessnica_backend.Dtos;
using Guessnica_backend.Models;
using Guessnica_backend.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Guessnica_backend.Integration.Test.Controllers;

public class GameControllerTests : IClassFixture<IntegrationTestGuessnicaFactory>, IAsyncLifetime, IDisposable
{
    private readonly HttpClient _client;
    private readonly AppDbContext _dbContext;
    private readonly IServiceScope _scope;
    private readonly IServiceProvider _serviceProvider;

    private string? _userToken;
    private string? _adminToken;
    private AppUser? _testUser;

    public GameControllerTests(IntegrationTestGuessnicaFactory factory)
    {
        _scope = factory.Services.CreateScope();
        _serviceProvider = _scope.ServiceProvider;
        _dbContext = _serviceProvider.GetRequiredService<AppDbContext>();
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await ResetGameDatabaseAsync();
        _userToken = await GetOrCreateUserTokenAsync();
        _adminToken = await GetOrCreateAdminTokenAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _scope?.Dispose();
        _client?.Dispose();
    }

    private async Task ResetGameDatabaseAsync()
    {
        await _dbContext.Database.ExecuteSqlRawAsync("""
            DELETE FROM "UserRiddles";
            DELETE FROM "Riddles";
            DELETE FROM "Locations";
            """);
    }

    private async Task<string> GetOrCreateUserTokenAsync()
    {
        if (_userToken is not null) return _userToken;

        using var scope = _serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();

        const string roleName = "User";
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }

        const string userEmail = "test-game-user@test.com";
        var user = await userManager.FindByEmailAsync(userEmail);

        if (user == null)
        {
            user = new AppUser
            {
                UserName = userEmail,
                Email = userEmail,
                DisplayName = "Test Game User",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(user, "GameUser123!");
            if (!result.Succeeded)
                throw new Exception($"Failed to create game user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        if (!await userManager.IsInRoleAsync(user, roleName))
        {
            await userManager.AddToRoleAsync(user, roleName);
        }

        _testUser = user;
        var tokenResponse = await jwtService.GenerateTokenAsync(user);
        return _userToken = tokenResponse.Token;
    }

    private async Task<string> GetOrCreateAdminTokenAsync()
    {
        if (_adminToken is not null) return _adminToken;

        using var scope = _serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();

        const string roleName = "Admin";
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }

        const string adminEmail = "test-game-admin@test.com";
        var admin = await userManager.FindByEmailAsync(adminEmail);

        if (admin == null)
        {
            admin = new AppUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                DisplayName = "Test Game Admin",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(admin, "GameAdmin123!");
            if (!result.Succeeded)
                throw new Exception($"Failed to create game admin: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        if (!await userManager.IsInRoleAsync(admin, roleName))
        {
            await userManager.AddToRoleAsync(admin, roleName);
        }

        var tokenResponse = await jwtService.GenerateTokenAsync(admin);
        return _adminToken = tokenResponse.Token;
    }

    private async Task<HttpClient> GetAuthorizedUserClientAsync()
    {
        var token = await GetOrCreateUserTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client;
    }

    private async Task<HttpClient> GetAuthorizedAdminClientAsync()
    {
        var token = await GetOrCreateAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client;
    }

    private async Task<Location> CreateTestLocationAsync()
    {
        var location = new Location
        {
            ShortDescription = "Test Game Location",
            Latitude = 52.4064m,
            Longitude = 16.9252m,
            ImageUrl = "https://example.com/test-image.jpg"
        };

        _dbContext.Locations.Add(location);
        await _dbContext.SaveChangesAsync();
        return location;
    }

    private async Task<Riddle> CreateTestRiddleAsync(int? locationId = null)
    {
        var location = locationId.HasValue
            ? await _dbContext.Locations.FindAsync(locationId.Value)
            : await CreateTestLocationAsync();

        var riddle = new Riddle
        {
            Description = "Test game riddle description",
            Difficulty = RiddleDifficulty.Medium,
            LocationId = location!.Id,
            TimeLimitSeconds = 300,
            MaxDistanceMeters = 1000
        };

        _dbContext.Riddles.Add(riddle);
        await _dbContext.SaveChangesAsync();

        return await _dbContext.Riddles
            .Include(r => r.Location)
            .FirstAsync(r => r.Id == riddle.Id);
    }

    private async Task<UserRiddle> CreateUserRiddleAsync(string userId, int riddleId, bool answered = false)
    {
        var userRiddle = new UserRiddle
        {
            UserId = userId,
            RiddleId = riddleId,
            AssignedAt = DateTime.UtcNow,
            AnsweredAt = answered ? DateTime.UtcNow : null
        };

        _dbContext.UserRiddles.Add(userRiddle);
        await _dbContext.SaveChangesAsync();

        return await _dbContext.UserRiddles
            .Include(ur => ur.Riddle)
            .ThenInclude(r => r.Location)
            .FirstAsync(ur => ur.Id == userRiddle.Id);
    }

    [Fact]
    public async Task GameController_GetDaily_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/game/daily");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GameController_GetDaily_WithValidUser_ReturnsOkWithRiddle()
    {
        var client = await GetAuthorizedUserClientAsync();
        var riddle = await CreateTestRiddleAsync();

        var response = await client.GetAsync("/game/daily");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<DailyRiddleResponseDto>();
        Assert.NotNull(result);
        Assert.Equal(riddle.Id, result.RiddleId);
        Assert.Equal(riddle.Location.ImageUrl, result.ImageUrl);
        Assert.Equal(riddle.Description, result.Description);
        Assert.Equal((int)riddle.Difficulty, result.Difficulty);
        Assert.Equal(riddle.TimeLimitSeconds, result.TimeLimitSeconds);
        Assert.Equal(riddle.MaxDistanceMeters, result.MaxDistanceMeters);
        Assert.False(result.IsAnswered);
    }

    [Fact]
    public async Task GameController_GetDaily_CreatesUserRiddleRecord()
    {
        var client = await GetAuthorizedUserClientAsync();
        await CreateTestRiddleAsync();

        var response = await client.GetAsync("/game/daily");
        var result = await response.Content.ReadFromJsonAsync<DailyRiddleResponseDto>();

        Assert.NotNull(result);
        Assert.NotEqual(0, result.UserRiddleId);

        var userRiddle = await _dbContext.UserRiddles
            .FirstOrDefaultAsync(ur => ur.Id == result.UserRiddleId);
        
        Assert.NotNull(userRiddle);
        Assert.Equal(_testUser!.Id, userRiddle.UserId);
        Assert.Null(userRiddle.AnsweredAt);
    }

    [Fact]
    public async Task GameController_GetDaily_ReturnsExistingUnansweredRiddle()
    {
        var client = await GetAuthorizedUserClientAsync();
        var riddle = await CreateTestRiddleAsync();
        var existingUserRiddle = await CreateUserRiddleAsync(_testUser!.Id, riddle.Id, answered: false);

        var response = await client.GetAsync("/game/daily");
        var result = await response.Content.ReadFromJsonAsync<DailyRiddleResponseDto>();

        Assert.NotNull(result);
        Assert.Equal(existingUserRiddle.Id, result.UserRiddleId);
        Assert.False(result.IsAnswered);
    }

    [Fact]
    public async Task GameController_SubmitAnswer_WithoutAuth_ReturnsUnauthorized()
    {
        var submitDto = new SubmitAnswerDto
        {
            Latitude = 52.4064m,
            Longitude = 16.9252m
        };

        var response = await _client.PostAsJsonAsync("/game/answer", submitDto);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GameController_SubmitAnswer_WithValidAnswer_ReturnsOkWithResult()
    {
        var client = await GetAuthorizedUserClientAsync();
        var riddle = await CreateTestRiddleAsync();
        await CreateUserRiddleAsync(_testUser!.Id, riddle.Id, answered: false);

        var submitDto = new SubmitAnswerDto
        {
            Latitude = riddle.Location.Latitude,
            Longitude = riddle.Location.Longitude
        };

        var response = await client.PostAsJsonAsync("/game/answer", submitDto);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<SubmitAnswerDto>();
        Assert.NotNull(result);
        Assert.True(result.Points >= 0);
        Assert.True(result.DistanceMeters >= 0);
        Assert.NotNull(result.TimeSeconds);
    }

    [Fact]
    public async Task GameController_SubmitAnswer_UpdatesUserRiddleAsAnswered()
    {
        var client = await GetAuthorizedUserClientAsync();
        var riddle = await CreateTestRiddleAsync();
        var userRiddle = await CreateUserRiddleAsync(_testUser!.Id, riddle.Id, answered: false);

        var submitDto = new SubmitAnswerDto
        {
            Latitude = riddle.Location.Latitude,
            Longitude = riddle.Location.Longitude
        };

        await client.PostAsJsonAsync("/game/answer", submitDto);

        _dbContext.ChangeTracker.Clear();
        var updatedUserRiddle = await _dbContext.UserRiddles.FindAsync(userRiddle.Id);
        
        Assert.NotNull(updatedUserRiddle);
        Assert.NotNull(updatedUserRiddle.AnsweredAt);
    }

    [Fact]
    public async Task GameController_SubmitAnswer_WhenNoActiveRiddle_ReturnsBadRequest()
    {
        var client = await GetAuthorizedUserClientAsync();
        
        var submitDto = new SubmitAnswerDto
        {
            Latitude = 52.4064m,
            Longitude = 16.9252m
        };

        var response = await client.PostAsJsonAsync("/game/answer", submitDto);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GameController_SubmitAnswer_WhenAlreadyAnswered_ReturnsBadRequest()
    {
        var client = await GetAuthorizedUserClientAsync();
        var riddle = await CreateTestRiddleAsync();
        await CreateUserRiddleAsync(_testUser!.Id, riddle.Id, answered: true);

        var submitDto = new SubmitAnswerDto
        {
            Latitude = riddle.Location.Latitude,
            Longitude = riddle.Location.Longitude
        };

        var response = await client.PostAsJsonAsync("/game/answer", submitDto);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GameController_SubmitAnswer_AsAdmin_ReturnsOk()
    {
        var client = await GetAuthorizedAdminClientAsync();
        var riddle = await CreateTestRiddleAsync();
        
        var adminUser = await _dbContext.Users.FirstAsync(u => u.Email == "test-game-admin@test.com");
        await CreateUserRiddleAsync(adminUser.Id, riddle.Id, answered: false);

        var submitDto = new SubmitAnswerDto
        {
            Latitude = riddle.Location.Latitude,
            Longitude = riddle.Location.Longitude
        };

        var response = await client.PostAsJsonAsync("/game/answer", submitDto);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GameController_SubmitAnswer_WithInvalidModel_ReturnsBadRequest()
    {
        var client = await GetAuthorizedUserClientAsync();
        
        var submitDto = new { };

        var response = await client.PostAsJsonAsync("/game/answer", submitDto);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
public async Task GameController_GameCompleteFlow_GetDailyAndSubmitAnswer_WorksCorrectly()
{
    var client = await GetAuthorizedUserClientAsync();
    var riddle = await CreateTestRiddleAsync();

    var getDailyResponse = await client.GetAsync("/game/daily");
    Assert.Equal(HttpStatusCode.OK, getDailyResponse.StatusCode);
    var dailyResult = await getDailyResponse.Content.ReadFromJsonAsync<DailyRiddleResponseDto>();
    Assert.NotNull(dailyResult);
    Assert.False(dailyResult.IsAnswered);

    var submitDto = new SubmitAnswerDto
    {
        Latitude = riddle.Location.Latitude,
        Longitude = riddle.Location.Longitude
    };

    var submitResponse = await client.PostAsJsonAsync("/game/answer", submitDto);
    Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

    _dbContext.ChangeTracker.Clear();
    var userRiddle = await _dbContext.UserRiddles
        .FirstOrDefaultAsync(ur => ur.Id == dailyResult.UserRiddleId);
    Assert.NotNull(userRiddle);
    Assert.NotNull(userRiddle.AnsweredAt);
    
    var secondDailyResponse = await client.GetAsync("/game/daily");

    if (secondDailyResponse.StatusCode == HttpStatusCode.OK)
    {
        var secondResult = await secondDailyResponse.Content.ReadFromJsonAsync<DailyRiddleResponseDto>();
        Assert.NotNull(secondResult);
        Assert.True(secondResult.IsAnswered);
    }
    else
    {
        Assert.Equal(HttpStatusCode.Conflict, secondDailyResponse.StatusCode);
    }

    var secondSubmitResponse = await client.PostAsJsonAsync("/game/answer", submitDto);
    Assert.Equal(HttpStatusCode.BadRequest, secondSubmitResponse.StatusCode);
}

    [Fact]
    public async Task GameController_SubmitAnswer_CalculatesDistanceCorrectly()
    {
        var client = await GetAuthorizedUserClientAsync();
        var riddle = await CreateTestRiddleAsync();
        await CreateUserRiddleAsync(_testUser!.Id, riddle.Id, answered: false);

        var submitDto = new SubmitAnswerDto
        {
            Latitude = riddle.Location.Latitude + 0.001m,
            Longitude = riddle.Location.Longitude + 0.001m
        };

        var response = await client.PostAsJsonAsync("/game/answer", submitDto);
        var result = await response.Content.ReadFromJsonAsync<SubmitAnswerDto>();

        Assert.NotNull(result);
        Assert.True(result.DistanceMeters > 0);
        Assert.True(result.DistanceMeters < riddle.MaxDistanceMeters * 2);
    }
}