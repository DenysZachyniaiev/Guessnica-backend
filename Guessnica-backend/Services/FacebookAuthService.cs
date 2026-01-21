using System.Net;
using Guessnica_backend.Dtos;

namespace Guessnica_backend.Services;

using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Guessnica_backend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

public interface IFacebookAuthService
{
    Task<(bool ok, string? userId)> ValidateAccessTokenAsync(string accessToken, CancellationToken ct = default);
    Task<FacebookUserInfo?> GetUserInfoAsync(string accessToken, CancellationToken ct = default);
    
    Task<TokenResponseDto> HandleFacebookLoginAsync(
        FacebookLoginDto dto,
        UserManager<AppUser> userManager,
        IJwtService jwtService,
        CancellationToken ct = default);
}


public record FacebookTokenDebugResponse(
    [property: JsonPropertyName("data")] FacebookTokenData Data
);

public record FacebookTokenData(
    [property: JsonPropertyName("is_valid")] bool IsValid,
    [property: JsonPropertyName("app_id")] string AppId,
    [property: JsonPropertyName("user_id")] string UserId
);

public record FacebookUserInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("email")] string? Email
);

public sealed class FacebookAuthService : IFacebookAuthService
{
    private readonly HttpClient _http;
    private readonly string _appId;
    private readonly string _appSecret;

    public FacebookAuthService(HttpClient http, IConfiguration cfg)
    {
        _http = http;
        _appId = cfg["Authentication:Facebook:AppId"] ?? throw new InvalidOperationException("Missing Facebook:AppId");
        _appSecret = cfg["Authentication:Facebook:AppSecret"] ?? throw new InvalidOperationException("Missing Facebook:AppSecret");
    }

    public async Task<(bool ok, string? userId)> ValidateAccessTokenAsync(string accessToken, CancellationToken ct = default)
    {
        var appAccessToken = $"{_appId}|{_appSecret}";
        var url = $"https://graph.facebook.com/debug_token?input_token={Uri.EscapeDataString(accessToken)}&access_token={Uri.EscapeDataString(appAccessToken)}";

        try
        {
            var resp = await _http.GetFromJsonAsync<FacebookTokenDebugResponse>(url, cancellationToken: ct);
            if (resp?.Data is null) return (false, null);

            var ok = resp.Data.IsValid && resp.Data.AppId == _appId;
            return (ok, resp.Data.UserId);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new UnauthorizedAccessException("Invalid Facebook Access Token", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("An error occurred while validating the access token", ex);
        }
    }

    public async Task<FacebookUserInfo?> GetUserInfoAsync(string accessToken, CancellationToken ct = default)
    {
        var fields = "id,name,email";
        var url = $"https://graph.facebook.com/me?fields={fields}&access_token={Uri.EscapeDataString(accessToken)}";

        try
        {
            var userInfo = await _http.GetFromJsonAsync<FacebookUserInfo>(url, cancellationToken: ct);

            if (userInfo?.Id == null)
                throw new InvalidOperationException("Facebook did not return user info");

            return userInfo;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException("Invalid Facebook access token", ex);
        }
    }

    public async Task<TokenResponseDto> HandleFacebookLoginAsync(
        FacebookLoginDto dto,
        UserManager<AppUser> userManager,
        IJwtService jwtService,
        CancellationToken ct = default)
    {
        var (ok, fbUserId) = await ValidateAccessTokenAsync(dto.AccessToken, ct);
        if (!ok || string.IsNullOrEmpty(fbUserId))
            throw new UnauthorizedAccessException("Invalid Facebook token");
        
        var profile = await GetUserInfoAsync(dto.AccessToken, ct);
        var email = profile?.Email;
        var displayName = profile?.Name ?? "Facebook User";
        
        var user = await userManager.FindByLoginAsync("Facebook", fbUserId)
                   ?? (!string.IsNullOrEmpty(email) ? await userManager.FindByEmailAsync(email) : null);
        
        if (user == null)
        {
            var effectiveEmail = email ?? $"fb_{fbUserId}@no-email.facebook.local";
            user = new AppUser
            {
                UserName = effectiveEmail,
                Email = email,
                DisplayName = displayName,
                EmailConfirmed = true
            };
            var createRes = await userManager.CreateAsync(user);
            if (!createRes.Succeeded)
            {
                throw new InvalidOperationException(
                    "Failed to create user: " + string.Join("; ", createRes.Errors.Select(e => e.Description))
                );
            }
            await userManager.AddToRoleAsync(user, "User");
        }
        else if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }
        
        var logins = await userManager.GetLoginsAsync(user);
        if (!logins.Any(l => l.LoginProvider == "Facebook" && l.ProviderKey == fbUserId))
        {
            await userManager.AddLoginAsync(user, new Microsoft.AspNetCore.Identity.UserLoginInfo("Facebook", fbUserId, "Facebook"));
        }
        
        var token = await jwtService.GenerateTokenAsync(user);

        return token;
    }
}

