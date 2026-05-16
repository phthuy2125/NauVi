using FoodWiseAi.Api.Models;
using Microsoft.AspNetCore.Http;

namespace FoodWiseAi.Api.DTOs;

public sealed class RegisterRequest
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public UserPreferences? Preferences { get; set; }
    public bool IsGoogle { get; set; }
    public string? GoogleToken { get; set; }
}

public sealed class LoginRequest
{
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public sealed class GoogleLoginRequest
{
    public string? Token { get; set; }
}

public sealed class UpdatePreferencesRequest
{
    public string? UserId { get; set; }
    public UserPreferences? Preferences { get; set; }
}

public sealed class UpdatePasswordRequest
{
    public string? UserId { get; set; }
    public string? CurrentPassword { get; set; }
    public string? NewPassword { get; set; }
}

public sealed class UpdateAvatarRequest
{
    public string? UserId { get; set; }
    public IFormFile? Avatar { get; set; }
}

public sealed class ToggleFavoriteRequest
{
    public string? UserId { get; set; }
    public string? FoodId { get; set; }
}

public sealed class RemoveBulkFavoritesRequest
{
    public List<string>? FoodIds { get; set; }
}
