using System.Text.Json;
using System.Text.RegularExpressions;

namespace FoodWiseAi.Api.Utilities;

public static class JsonBlockExtractor
{
    public static T ExtractObject<T>(string raw)
    {
        var cleaned = Cleanup(raw);
        try
        {
            return JsonSerializer.Deserialize<T>(cleaned, JsonOptions)!;
        }
        catch
        {
            var match = Regex.Match(cleaned, @"\{[\s\S]*\}");
            if (!match.Success)
            {
                throw new InvalidOperationException("Không tìm thấy block JSON object trong phản hồi AI.");
            }
            return JsonSerializer.Deserialize<T>(match.Value, JsonOptions)!;
        }
    }

    public static T ExtractArray<T>(string raw)
    {
        var cleaned = Cleanup(raw);
        try
        {
            return JsonSerializer.Deserialize<T>(cleaned, JsonOptions)!;
        }
        catch
        {
            var match = Regex.Match(cleaned, @"\[[\s\S]*\]");
            if (!match.Success)
            {
                throw new InvalidOperationException("Không tìm thấy block JSON array trong phản hồi AI.");
            }
            return JsonSerializer.Deserialize<T>(match.Value, JsonOptions)!;
        }
    }

    private static string Cleanup(string raw)
    {
        return raw.Trim()
            .Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("```", string.Empty)
            .Trim();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
