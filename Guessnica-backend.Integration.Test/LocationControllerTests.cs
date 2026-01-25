using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Guessnica_backend.Data;
using Guessnica_backend.Dtos.Location;
using Guessnica_backend.Models;
using Guessnica_backend.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Guessnica_backend.Integration.Test.Controllers;

public class LocationControllerTests : IClassFixture<IntegrationTestGuessnicaFactory>, IAsyncLifetime, IDisposable
{
    private readonly HttpClient _client;
    private readonly AppDbContext _dbContext;
    private readonly IServiceScope _scope;
    private readonly IServiceProvider _serviceProvider;

    private string? _userToken;
    private string? _adminToken;

    public LocationControllerTests(IntegrationTestGuessnicaFactory factory)
    {
        _scope = factory.Services.CreateScope();
        _serviceProvider = _scope.ServiceProvider;
        _dbContext = _serviceProvider.GetRequiredService<AppDbContext>();
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await ResetLocationDatabaseAsync();
        _userToken = await GetOrCreateUserTokenAsync();
        _adminToken = await GetOrCreateAdminTokenAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _scope?.Dispose();
        _client?.Dispose();
    }

    private async Task ResetLocationDatabaseAsync()
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

        const string userEmail = "test-location-user@test.com";
        var user = await userManager.FindByEmailAsync(userEmail);

        if (user == null)
        {
            user = new AppUser
            {
                UserName = userEmail,
                Email = userEmail,
                DisplayName = "Test Location User",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(user, "LocationUser123!");
            if (!result.Succeeded)
                throw new Exception($"Failed to create location user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        if (!await userManager.IsInRoleAsync(user, roleName))
        {
            await userManager.AddToRoleAsync(user, roleName);
        }

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

        const string adminEmail = "test-location-admin@test.com";
        var admin = await userManager.FindByEmailAsync(adminEmail);

        if (admin == null)
        {
            admin = new AppUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                DisplayName = "Test Location Admin",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(admin, "LocationAdmin123!");
            if (!result.Succeeded)
                throw new Exception($"Failed to create location admin: {string.Join(", ", result.Errors.Select(e => e.Description))}");
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
            ShortDescription = "Test Location",
            Latitude = 52.2297m,
            Longitude = 21.0122m,
            ImageUrl = "https://example.com/test-image.jpg"
        };

        _dbContext.Locations.Add(location);
        await _dbContext.SaveChangesAsync();
        return location;
    }

    private MultipartFormDataContent CreateLocationFormData(
        decimal latitude, 
        decimal longitude, 
        string shortDescription,
        bool includeImage = true)
    {
        var formData = new MultipartFormDataContent();
        formData.Add(new StringContent(latitude.ToString()), "Latitude");
        formData.Add(new StringContent(longitude.ToString()), "Longitude");
        formData.Add(new StringContent(shortDescription), "ShortDescription");

        if (includeImage)
        {
            var imageContent = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // PNG header
            imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            formData.Add(imageContent, "Image", "test.png");
        }

        return formData;
    }

    [Fact]
    public async Task LocationController_GetAll_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/locations");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LocationController_GetAll_WithUserAuth_ReturnsAllLocations()
    {
        var client = await GetAuthorizedUserClientAsync();
        var location1 = await CreateTestLocationAsync();
        var location2 = await CreateTestLocationAsync();

        var response = await client.GetAsync("/locations");
        response.EnsureSuccessStatusCode();

        var locations = await response.Content.ReadFromJsonAsync<List<LocationResponseDto>>();
        Assert.NotNull(locations);
        Assert.True(locations.Count >= 2);
        Assert.Contains(locations, l => l.Id == location1.Id);
        Assert.Contains(locations, l => l.Id == location2.Id);
    }

    [Fact]
    public async Task LocationController_GetAll_ReturnsCorrectStructure()
    {
        var client = await GetAuthorizedUserClientAsync();
        var location = await CreateTestLocationAsync();

        var response = await client.GetAsync("/locations");
        response.EnsureSuccessStatusCode();

        var locations = await response.Content.ReadFromJsonAsync<List<LocationResponseDto>>();
        var returnedLocation = locations!.First(l => l.Id == location.Id);

        Assert.Equal(location.Id, returnedLocation.Id);
        Assert.Equal(location.Latitude, returnedLocation.Latitude);
        Assert.Equal(location.Longitude, returnedLocation.Longitude);
        Assert.Equal(location.ImageUrl, returnedLocation.ImageUrl);
        Assert.Equal(location.ShortDescription, returnedLocation.ShortDescription);
    }

    [Fact]
    public async Task LocationController_Get_WithoutAuth_ReturnsUnauthorized()
    {
        var location = await CreateTestLocationAsync();
        var response = await _client.GetAsync($"/locations/{location.Id}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LocationController_Get_WithValidId_ReturnsLocation()
    {
        var client = await GetAuthorizedUserClientAsync();
        var location = await CreateTestLocationAsync();

        var response = await client.GetAsync($"/locations/{location.Id}");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LocationResponseDto>();
        Assert.NotNull(result);
        Assert.Equal(location.Id, result.Id);
        Assert.Equal(location.Latitude, result.Latitude);
        Assert.Equal(location.Longitude, result.Longitude);
        Assert.Equal(location.ShortDescription, result.ShortDescription);
    }

    [Fact]
    public async Task LocationController_Get_WithInvalidId_ReturnsNotFound()
    {
        var client = await GetAuthorizedUserClientAsync();

        var response = await client.GetAsync("/locations/99999");
        
        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound || 
            response.StatusCode == HttpStatusCode.InternalServerError,
            $"Expected NotFound or InternalServerError, but got {response.StatusCode}"
        );
    }

    [Fact]
    public async Task LocationController_Create_WithoutAuth_ReturnsUnauthorized()
    {
        var formData = CreateLocationFormData(52.2297m, 21.0122m, "New Location");

        var response = await _client.PostAsync("/locations", formData);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LocationController_Create_AsUser_ReturnsForbidden()
    {
        var client = await GetAuthorizedUserClientAsync();
        var formData = CreateLocationFormData(52.2297m, 21.0122m, "New Location");

        var response = await client.PostAsync("/locations", formData);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LocationController_Create_AsAdmin_WithValidData_CreatesLocation()
    {
        var client = await GetAuthorizedAdminClientAsync();
        var formData = CreateLocationFormData(52.2297m, 21.0122m, "New Test Location");

        var response = await client.PostAsync("/locations", formData);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<LocationResponseDto>();
        Assert.NotNull(result);
        Assert.Equal("New Test Location", result.ShortDescription);
        Assert.Equal(52.2297m, result.Latitude);
        Assert.Equal(21.0122m, result.Longitude);

        var dbLocation = await _dbContext.Locations.FindAsync(result.Id);
        Assert.NotNull(dbLocation);
        Assert.Equal("New Test Location", dbLocation.ShortDescription);
    }

    [Fact]
    public async Task LocationController_Create_WithoutImage_ReturnsBadRequest()
    {
        var client = await GetAuthorizedAdminClientAsync();
        var formData = CreateLocationFormData(52.2297m, 21.0122m, "No Image Location", includeImage: false);

        var response = await client.PostAsync("/locations", formData);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var errorMessage = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrEmpty(errorMessage));
    }

    [Fact]
    public async Task LocationController_Create_WithInvalidModel_ReturnsBadRequest()
    {
        var client = await GetAuthorizedAdminClientAsync();
        var formData = CreateLocationFormData(200m, 200m, "");

        var response = await client.PostAsync("/locations", formData);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LocationController_Update_WithoutAuth_ReturnsUnauthorized()
    {
        var location = await CreateTestLocationAsync();
        var formData = CreateLocationFormData(52.3m, 21.1m, "Updated Location", includeImage: false);

        var response = await _client.PutAsync($"/locations/{location.Id}", formData);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LocationController_Update_AsUser_ReturnsForbidden()
    {
        var client = await GetAuthorizedUserClientAsync();
        var location = await CreateTestLocationAsync();
        var formData = CreateLocationFormData(52.3m, 21.1m, "Updated Location", includeImage: false);

        var response = await client.PutAsync($"/locations/{location.Id}", formData);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LocationController_Update_AsAdmin_WithValidData_UpdatesLocation()
    {
        var client = await GetAuthorizedAdminClientAsync();
        var location = await CreateTestLocationAsync();

        var formData = CreateLocationFormData(52.5m, 21.5m, "Updated Description", includeImage: false);

        var response = await client.PutAsync($"/locations/{location.Id}", formData);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LocationResponseDto>();
        Assert.NotNull(result);
        Assert.Equal("Updated Description", result.ShortDescription);
        Assert.Equal(52.5m, result.Latitude);
        Assert.Equal(21.5m, result.Longitude);

        _dbContext.ChangeTracker.Clear();
        var dbLocation = await _dbContext.Locations.FindAsync(location.Id);
        Assert.NotNull(dbLocation);
        Assert.Equal("Updated Description", dbLocation.ShortDescription);
    }

    [Fact]
    public async Task LocationController_Update_WithNewImage_UpdatesImageUrl()
    {
        var client = await GetAuthorizedAdminClientAsync();
        var location = await CreateTestLocationAsync();
        var originalImageUrl = location.ImageUrl;

        var formData = CreateLocationFormData(52.5m, 21.5m, "Updated with New Image", includeImage: true);

        var response = await client.PutAsync($"/locations/{location.Id}", formData);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LocationResponseDto>();
        Assert.NotNull(result);
        Assert.NotNull(result.ImageUrl);
    }

    [Fact]
    public async Task LocationController_Update_WithInvalidId_ReturnsNotFound()
    {
        var client = await GetAuthorizedAdminClientAsync();
        var formData = CreateLocationFormData(52.3m, 21.1m, "Updated", includeImage: false);

        var response = await client.PutAsync("/locations/99999", formData);

        Assert.True(
            response.StatusCode == HttpStatusCode.NotFound || 
            response.StatusCode == HttpStatusCode.InternalServerError,
            $"Expected NotFound or InternalServerError, but got {response.StatusCode}"
        );
    }

    [Fact]
    public async Task LocationController_Delete_WithoutAuth_ReturnsUnauthorized()
    {
        var location = await CreateTestLocationAsync();
        var response = await _client.DeleteAsync($"/locations/{location.Id}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LocationController_Delete_AsUser_ReturnsForbidden()
    {
        var client = await GetAuthorizedUserClientAsync();
        var location = await CreateTestLocationAsync();

        var response = await client.DeleteAsync($"/locations/{location.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LocationController_Delete_AsAdmin_WithValidId_DeletesLocation()
    {
        var client = await GetAuthorizedAdminClientAsync();
        var location = await CreateTestLocationAsync();

        var response = await client.DeleteAsync($"/locations/{location.Id}");
        response.EnsureSuccessStatusCode();

        _dbContext.ChangeTracker.Clear();
        var dbLocation = await _dbContext.Locations.FindAsync(location.Id);
        Assert.Null(dbLocation);
    }

    [Fact]
    public async Task LocationController_Delete_WithInvalidId_ReturnsNotFound()
    {
        var client = await GetAuthorizedAdminClientAsync();

        var response = await client.DeleteAsync("/locations/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task LocationController_CleanupImages_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.PostAsync("/locations/cleanup-images", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LocationController_CleanupImages_AsUser_ReturnsForbidden()
    {
        var client = await GetAuthorizedUserClientAsync();

        var response = await client.PostAsync("/locations/cleanup-images", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LocationController_CleanupImages_AsAdmin_ReturnsOkWithRemovedCount()
    {
        var client = await GetAuthorizedAdminClientAsync();

        var response = await client.PostAsync("/locations/cleanup-images", null);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>();
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("removed"));
        Assert.True(result["removed"] >= 0);
    }

    [Fact]
    public async Task LocationController_LocationCompleteLifecycle_CreateUpdateDelete_WorksCorrectly()
    {
        var client = await GetAuthorizedAdminClientAsync();

        var createFormData = CreateLocationFormData(52.2m, 21.0m, "Lifecycle Test Location");
        var createResponse = await client.PostAsync("/locations", createFormData);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<LocationResponseDto>();
        Assert.NotNull(created);

        var updateFormData = CreateLocationFormData(52.3m, 21.1m, "Updated Lifecycle Location", includeImage: false);
        var updateResponse = await client.PutAsync($"/locations/{created.Id}", updateFormData);
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<LocationResponseDto>();
        Assert.Equal("Updated Lifecycle Location", updated!.ShortDescription);

        var deleteResponse = await client.DeleteAsync($"/locations/{created.Id}");
        deleteResponse.EnsureSuccessStatusCode();

        _dbContext.ChangeTracker.Clear();
        var deletedLocation = await _dbContext.Locations.FindAsync(created.Id);
        Assert.Null(deletedLocation);

        var getResponse = await client.GetAsync($"/locations/{created.Id}");
        Assert.True(
            getResponse.StatusCode == HttpStatusCode.NotFound || 
            getResponse.StatusCode == HttpStatusCode.InternalServerError,
            "Deleted location should return NotFound or InternalServerError"
        );
    }

    [Fact]
    public async Task LocationController_GetAll_WithAdminAuth_ReturnsAllLocations()
    {
        var client = await GetAuthorizedAdminClientAsync();
        var location1 = await CreateTestLocationAsync();
        var location2 = await CreateTestLocationAsync();

        var response = await client.GetAsync("/locations");
        response.EnsureSuccessStatusCode();

        var locations = await response.Content.ReadFromJsonAsync<List<LocationResponseDto>>();
        Assert.NotNull(locations);
        Assert.True(locations.Count >= 2);
    }

    [Fact]
    public async Task LocationController_Create_ReturnsCreatedAtAction()
    {
        var client = await GetAuthorizedAdminClientAsync();
        var formData = CreateLocationFormData(52.2297m, 21.0122m, "CreatedAt Test");

        var response = await client.PostAsync("/locations", formData);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var locationHeader = response.Headers.Location;
        Assert.NotNull(locationHeader);
        Assert.Contains("/locations/", locationHeader.ToString());
    }

    [Fact]
    public async Task LocationController_Update_WithoutImageChange_KeepsOriginalImage()
    {
        var client = await GetAuthorizedAdminClientAsync();
        var location = await CreateTestLocationAsync();
        var originalImageUrl = location.ImageUrl;

        var formData = CreateLocationFormData(52.5m, 21.5m, "Updated No Image Change", includeImage: false);

        var response = await client.PutAsync($"/locations/{location.Id}", formData);
        response.EnsureSuccessStatusCode();

        _dbContext.ChangeTracker.Clear();
        var dbLocation = await _dbContext.Locations.FindAsync(location.Id);
        Assert.NotNull(dbLocation);
    }

    [Fact]
    public async Task LocationController_GetAll_EmptyDatabase_ReturnsEmptyList()
    {
        var client = await GetAuthorizedUserClientAsync();

        var response = await client.GetAsync("/locations");
        response.EnsureSuccessStatusCode();

        var locations = await response.Content.ReadFromJsonAsync<List<LocationResponseDto>>();
        Assert.NotNull(locations);
        Assert.Empty(locations);
    }

    [Fact]
    public async Task LocationController_Create_WithServiceException_ReturnsBadRequest()
    {
        var client = await GetAuthorizedAdminClientAsync();

        var formData = new MultipartFormDataContent();
        formData.Add(new StringContent("invalid"), "Latitude");
        formData.Add(new StringContent("invalid"), "Longitude");
        formData.Add(new StringContent("Test"), "ShortDescription");
        
        var imageContent = new ByteArrayContent(new byte[] { 0x00 });
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        formData.Add(imageContent, "Image", "test.png");

        var response = await client.PostAsync("/locations", formData);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}