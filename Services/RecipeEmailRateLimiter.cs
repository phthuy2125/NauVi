using System.Collections.Concurrent;

namespace FoodWiseAi.Api.Services;

/// <summary>
/// Bộ nhớ tạm giới hạn tần suất gửi email: tối đa maxRequests trong khoảng thời gian windowMinutes.
/// Mặc định: tối đa 5 email / 10 phút trên mỗi IP/Client.
/// </summary>
public sealed class RecipeEmailRateLimiter : IRecipeEmailRateLimiter
{
    private readonly int _maxRequests;
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<string, List<DateTime>> _records = new();

    public RecipeEmailRateLimiter(int maxRequests = 5, int windowMinutes = 10)
    {
        _maxRequests = maxRequests;
        _window = TimeSpan.FromMinutes(windowMinutes);
    }

    public bool IsAllowed(string clientKey)
    {
        if (string.IsNullOrWhiteSpace(clientKey)) clientKey = "anonymous";

        var now = DateTime.UtcNow;
        var timestamps = _records.GetOrAdd(clientKey, _ => new List<DateTime>());

        lock (timestamps)
        {
            // Xóa các lần gọi đã quá thời hạn 10 phút
            timestamps.RemoveAll(t => now - t > _window);

            if (timestamps.Count >= _maxRequests)
            {
                return false;
            }

            timestamps.Add(now);
            return true;
        }
    }
}
