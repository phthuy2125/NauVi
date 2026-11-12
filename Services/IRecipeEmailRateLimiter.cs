namespace FoodWiseAi.Api.Services;

/// <summary>
/// Giao diện giới hạn tần suất gửi email công thức.
/// </summary>
public interface IRecipeEmailRateLimiter
{
    bool IsAllowed(string clientKey);
}
