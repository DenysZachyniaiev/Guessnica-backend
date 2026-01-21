using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Guessnica_backend.Data;
using Guessnica_backend.Dtos.Riddle;
using Guessnica_backend.Models;
using Guessnica_backend.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Guessnica_backend.Integration.Test.Controllers;

public class RiddlesControllerTests : IClassFixture<IntegrationTestGuessnicaFactory>, IAsyncLifetime, IDisposable
{
    private readonly HttpClient _client;
    private readonly AppDbContext _dbContext;
    private readonly IServiceScope _scope;
    private readonly IServiceProvider _serviceProvider;

    private string? _adminToken;

    public RiddlesControllerTests(IntegrationTestGuessnicaFactory factory)
    {
        _scope = factory.Services.CreateScope();
        _serviceProvider = _scope.ServiceProvider;
        _dbContext = _serviceProvider.GetRequiredService<AppDbContext>();
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await ResetRiddlesDatabaseAsync();
        _adminToken = await GetOrCreateRiddlesAdminTokenAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _scope?.Dispose();
        _client?.Dispose();
    }

    private async Task ResetRiddlesDatabaseAsync()
    {
        await _dbContext.Database.ExecuteSqlRawAsync("""
            DELETE FROM "Riddles";
            DELETE FROM "Locations";
            """);
    }

    private async Task<string> GetOrCreateRiddlesAdminTokenAsync()
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

        const string adminEmail = "test-admin-riddles@test.com";
        var admin = await userManager.FindByEmailAsync(adminEmail);

        if (admin == null)
        {
            admin = new AppUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                DisplayName = "Test Riddles Admin",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(admin, "RiddlesAdmin123!");
            if (!result.Succeeded)
                throw new Exception($"Failed to create riddles admin: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        if (!await userManager.IsInRoleAsync(admin, roleName))
        {
            await userManager.AddToRoleAsync(admin, roleName);
        }

        var tokenResponse = await jwtService.GenerateTokenAsync(admin);
        return _adminToken = tokenResponse.Token;
    }

    private async Task<HttpClient> GetAuthorizedRiddlesClientAsync()
    {
        var token = await GetOrCreateRiddlesAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client;
    }

    private async Task<Location> CreateTestRiddleLocationAsync()
    {
        var location = new Location
        {
            ShortDescription = "Test Riddle Location",
            Latitude = 52.2297m,
            Longitude = 21.0122m,
            ImageUrl = "https://as2.ftcdn.net/v2/jpg/07/65/92/49/1000_F_765924935_qEOZJabgejorVnLffKDKDhkUZVSFpMRV.jpg"
        };

        _dbContext.Locations.Add(location);
        await _dbContext.SaveChangesAsync();
        return location;
    }

    private async Task<Riddle> CreateTestRiddleAsync(int? locationId = null)
    {
        var location = locationId.HasValue
            ? await _dbContext.Locations.FindAsync(locationId.Value)
            : await CreateTestRiddleLocationAsync();

        var riddle = new Riddle
        {
            Description = "Test riddle description",
            Difficulty = RiddleDifficulty.Medium,
            LocationId = location!.Id,
            TimeLimitSeconds = 300,
            MaxDistanceMeters = 100
        };

        _dbContext.Riddles.Add(riddle);
        await _dbContext.SaveChangesAsync();

        return await _dbContext.Riddles
            .Include(r => r.Location)
            .FirstAsync(r => r.Id == riddle.Id);
    }

    [Fact]
    public async Task RiddlesController_GetAllRiddles_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/riddles");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RiddlesController_GetAllRiddles_WithAdminAuth_ReturnsAllRiddles()
    {
        var client = await GetAuthorizedRiddlesClientAsync();
        var riddle1 = await CreateTestRiddleAsync();
        var riddle2 = await CreateTestRiddleAsync();

        var response = await client.GetAsync("/riddles");
        response.EnsureSuccessStatusCode();

        var riddles = await response.Content.ReadFromJsonAsync<List<RiddleResponseDto>>();
        Assert.NotNull(riddles);
        Assert.True(riddles.Count >= 2);
        Assert.Contains(riddles, r => r.Id == riddle1.Id);
        Assert.Contains(riddles, r => r.Id == riddle2.Id);
    }

    [Fact]
    public async Task RiddlesController_GetAllRiddles_ReturnsCorrectStructure()
    {
        var client = await GetAuthorizedRiddlesClientAsync();
        var riddle = await CreateTestRiddleAsync();

        var response = await client.GetAsync("/riddles");
        response.EnsureSuccessStatusCode();

        var riddles = await response.Content.ReadFromJsonAsync<List<RiddleResponseDto>>();
        var returnedRiddle = riddles!.First(r => r.Id == riddle.Id);

        Assert.Equal(riddle.Description, returnedRiddle.Description);
        Assert.Equal((int)riddle.Difficulty, returnedRiddle.Difficulty);
        Assert.Equal(riddle.LocationId, returnedRiddle.LocationId);
        Assert.Equal(riddle.Location.ShortDescription, returnedRiddle.ShortDescription);
        Assert.Equal(riddle.Location.Latitude, returnedRiddle.Latitude);
        Assert.Equal(riddle.Location.Longitude, returnedRiddle.Longitude);
        Assert.Equal(riddle.TimeLimitSeconds, returnedRiddle.TimeLimitSeconds);
        Assert.Equal(riddle.MaxDistanceMeters, returnedRiddle.MaxDistanceMeters);
    }

    [Fact]
    public async Task RiddlesController_GetRiddle_WithoutAuth_ReturnsUnauthorized()
    {
        var riddle = await CreateTestRiddleAsync();
        var response = await _client.GetAsync($"/riddles/{riddle.Id}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RiddlesController_GetRiddle_WithValidId_ReturnsRiddle()
    {
        var client = await GetAuthorizedRiddlesClientAsync();
        var riddle = await CreateTestRiddleAsync();

        var response = await client.GetAsync($"/riddles/{riddle.Id}");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<RiddleResponseDto>();
        Assert.NotNull(result);
        Assert.Equal(riddle.Id, result.Id);
        Assert.Equal(riddle.Description, result.Description);
        Assert.Equal((int)riddle.Difficulty, result.Difficulty);
    }

    [Fact]
    public async Task RiddlesController_GetRiddle_WithInvalidId_ReturnsNotFound()
    {
        var client = await GetAuthorizedRiddlesClientAsync();

        var response = await client.GetAsync("/riddles/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RiddlesController_CreateRiddle_WithoutAuth_ReturnsUnauthorized()
    {
        var location = await CreateTestRiddleLocationAsync();
        var dto = new RiddleCreateDto
        {
            Description = "New riddle",
            Difficulty = 2,
            LocationId = location.Id,
            TimeLimitSeconds = 300,
            MaxDistanceMeters = 100
        };

        var response = await _client.PostAsJsonAsync("/riddles", dto);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RiddlesController_CreateRiddle_WithValidData_CreatesRiddle()
    {
        var client = await GetAuthorizedRiddlesClientAsync();
        var location = await CreateTestRiddleLocationAsync();

        var dto = new RiddleCreateDto
        {
            Description = "New test riddle",
            Difficulty = 2,
            LocationId = location.Id,
            TimeLimitSeconds = 600,
            MaxDistanceMeters = 200
        };

        var response = await client.PostAsJsonAsync("/riddles", dto);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<RiddleResponseDto>();
        Assert.NotNull(result);
        Assert.Equal(dto.Description, result.Description);

        var dbRiddle = await _dbContext.Riddles.FindAsync(result.Id);
        Assert.NotNull(dbRiddle);
        Assert.Equal(dto.Description, dbRiddle.Description);
    }

    [Fact]
    public async Task RiddlesController_CreateRiddle_WithInvalidLocation_ReturnsBadRequest()
    {
        var client = await GetAuthorizedRiddlesClientAsync();
        var dto = new RiddleCreateDto
        {
            Description = "New riddle",
            Difficulty = 2,
            LocationId = 99999,
            TimeLimitSeconds = 300,
            MaxDistanceMeters = 100
        };

        var response = await client.PostAsJsonAsync("/riddles", dto);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RiddlesController_CreateRiddle_WithInvalidModel_ReturnsBadRequest()
    {
        var client = await GetAuthorizedRiddlesClientAsync();
        var dto = new RiddleCreateDto
        {
            Description = "",
            Difficulty = 2,
            LocationId = 1,
            TimeLimitSeconds = 300,
            MaxDistanceMeters = 100
        };

        var response = await client.PostAsJsonAsync("/riddles", dto);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RiddlesController_UpdateRiddle_WithoutAuth_ReturnsUnauthorized()
    {
        var riddle = await CreateTestRiddleAsync();
        var dto = new RiddleUpdateDto
        {
            Description = "Updated description",
            Difficulty = 3,
            LocationId = riddle.LocationId,
            TimeLimitSeconds = 400,
            MaxDistanceMeters = 150
        };

        var response = await _client.PutAsJsonAsync($"/riddles/{riddle.Id}", dto);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RiddlesController_UpdateRiddle_WithValidData_UpdatesRiddle()
    {
        var client = await GetAuthorizedRiddlesClientAsync();
        var riddle = await CreateTestRiddleAsync();

        var dto = new RiddleUpdateDto
        {
            Description = "Updated description",
            Difficulty = 3,
            LocationId = riddle.LocationId,
            TimeLimitSeconds = 400,
            MaxDistanceMeters = 150
        };

        var response = await client.PutAsJsonAsync($"/riddles/{riddle.Id}", dto);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<RiddleResponseDto>();
        Assert.NotNull(result);
        Assert.Equal(dto.Description, result.Description);

        _dbContext.ChangeTracker.Clear();
        var dbRiddle = await _dbContext.Riddles.FindAsync(riddle.Id);
        Assert.NotNull(dbRiddle);
        Assert.Equal(dto.Description, dbRiddle.Description);
    }

    [Fact]
    public async Task RiddlesController_UpdateRiddle_WithInvalidId_ReturnsNotFound()
    {
        var client = await GetAuthorizedRiddlesClientAsync();
        var location = await CreateTestRiddleLocationAsync();

        var dto = new RiddleUpdateDto
        {
            Description = "Updated",
            Difficulty = 2,
            LocationId = location.Id,
            TimeLimitSeconds = 300,
            MaxDistanceMeters = 100
        };

        var response = await client.PutAsJsonAsync("/riddles/99999", dto);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RiddlesController_UpdateRiddle_WithInvalidLocation_ReturnsBadRequest()
    {
        var client = await GetAuthorizedRiddlesClientAsync();
        var riddle = await CreateTestRiddleAsync();

        var dto = new RiddleUpdateDto
        {
            Description = "Updated",
            Difficulty = 2,
            LocationId = 99999,
            TimeLimitSeconds = 300,
            MaxDistanceMeters = 100
        };

        var response = await client.PutAsJsonAsync($"/riddles/{riddle.Id}", dto);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RiddlesController_DeleteRiddle_WithoutAuth_ReturnsUnauthorized()
    {
        var riddle = await CreateTestRiddleAsync();
        var response = await _client.DeleteAsync($"/riddles/{riddle.Id}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RiddlesController_DeleteRiddle_WithValidId_DeletesRiddle()
    {
        var client = await GetAuthorizedRiddlesClientAsync();
        var riddle = await CreateTestRiddleAsync();

        var response = await client.DeleteAsync($"/riddles/{riddle.Id}");
        response.EnsureSuccessStatusCode();

        _dbContext.ChangeTracker.Clear();
        var dbRiddle = await _dbContext.Riddles.FindAsync(riddle.Id);
        Assert.Null(dbRiddle);
    }

    [Fact]
    public async Task RiddlesController_DeleteRiddle_WithInvalidId_ReturnsNotFound()
    {
        var client = await GetAuthorizedRiddlesClientAsync();
        var response = await client.DeleteAsync("/riddles/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RiddlesController_RiddleCompleteLifecycle_CreateUpdateDelete_WorksCorrectly()
    {
        var client = await GetAuthorizedRiddlesClientAsync();
        var location = await CreateTestRiddleLocationAsync();

        var createDto = new RiddleCreateDto
        {
            Description = "Lifecycle test riddle",
            Difficulty = 1,
            LocationId = location.Id,
            TimeLimitSeconds = 300,
            MaxDistanceMeters = 100
        };

        var createResponse = await client.PostAsJsonAsync("/riddles", createDto);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<RiddleResponseDto>();

        var updateDto = new RiddleUpdateDto
        {
            Description = "Updated lifecycle riddle",
            Difficulty = 3,
            LocationId = location.Id,
            TimeLimitSeconds = 500,
            MaxDistanceMeters = 200
        };

        var updateResponse = await client.PutAsJsonAsync($"/riddles/{created!.Id}", updateDto);
        updateResponse.EnsureSuccessStatusCode();

        var deleteResponse = await client.DeleteAsync($"/riddles/{created.Id}");
        deleteResponse.EnsureSuccessStatusCode();

        var getResponse = await client.GetAsync($"/riddles/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }
}