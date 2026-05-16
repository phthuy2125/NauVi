using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FoodWiseAi.Api.Services;

public sealed class GeminiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public GeminiService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public bool HasApiKey => !string.IsNullOrWhiteSpace(GetApiKey());

    public async Task<string> GenerateTextAsync(
        string modelName,
        IEnumerable<GeminiContent> contents,
        string? systemInstruction = null,
        GeminiGenerationConfig? generationConfig = null,
        object? tools = null)
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new GeminiApiException(HttpStatusCode.InternalServerError, "Chưa cấu hình GEMINI_API_KEY.");
        }
        // Temporary debug logging (masked key + request info)
        try
        {
            var masked = apiKey.Length > 10 ? apiKey.Substring(0, 6) + "..." + apiKey.Substring(apiKey.Length - 4) : apiKey;
            Console.WriteLine($"[GeminiService] Using GEMINI_API_KEY: {masked}");
            Console.WriteLine($"[GeminiService] Model: {modelName}, MaxOutputTokens: {generationConfig?.MaxOutputTokens ?? 0}");
        }
        catch { /* ignore logging errors */ }

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";
        var body = new Dictionary<string, object?>
        {
            ["contents"] = contents,
            ["generationConfig"] = generationConfig,
            ["tools"] = tools
        };

        if (!string.IsNullOrWhiteSpace(systemInstruction))
        {
            body["systemInstruction"] = new { parts = new[] { new { text = systemInstruction } } };
        }

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        using var response = await _httpClient.PostAsJsonAsync(url, body, jsonOptions);
        var payload = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new GeminiApiException(response.StatusCode, payload);
        }

        using var document = JsonDocument.Parse(payload);
        if (document.RootElement.TryGetProperty("candidates", out var candidates)
            && candidates.ValueKind == JsonValueKind.Array
            && candidates.GetArrayLength() > 0)
        {
            var first = candidates[0];
            if (first.TryGetProperty("content", out var content)
                && content.TryGetProperty("parts", out var parts)
                && parts.ValueKind == JsonValueKind.Array)
            {
                var textParts = parts.EnumerateArray()
                    .Where(p => p.TryGetProperty("text", out _))
                    .Select(p => p.GetProperty("text").GetString())
                    .Where(text => !string.IsNullOrWhiteSpace(text));

                var combined = string.Join("\n", textParts!);
                if (!string.IsNullOrWhiteSpace(combined))
                {
                    return combined;
                }
            }
        }

        throw new GeminiApiException(HttpStatusCode.InternalServerError, "Gemini trả về dữ liệu rỗng hoặc không đúng định dạng.");
    }

    public Task<string> GenerateTextAsync(
        string modelName,
        string prompt,
        string? systemInstruction = null,
        GeminiGenerationConfig? generationConfig = null,
        object? tools = null)
    {
        var contents = new[]
        {
            new GeminiContent("user", new[] { new GeminiPart(prompt) })
        };
        return GenerateTextAsync(modelName, contents, systemInstruction, generationConfig, tools);
    }

    public async Task<List<string>> ListModelsAsync()
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new GeminiApiException(HttpStatusCode.InternalServerError, "Chưa cấu hình GEMINI_API_KEY.");
        }

        var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
        using var response = await _httpClient.GetAsync(url);
        var payload = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new GeminiApiException(response.StatusCode, payload);
        }

        using var document = JsonDocument.Parse(payload);
        if (!document.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array)
        {
            return new List<string>();
        }

        return models.EnumerateArray()
            .Select(model => model.TryGetProperty("name", out var name) ? name.GetString() : null)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Replace("models/", string.Empty))
            .ToList();
    }



    private string? GetApiKey()
        => _configuration["GEMINI_API_KEY"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
}

public sealed class GeminiApiException : Exception
{
    public GeminiApiException(HttpStatusCode statusCode, string detail)
        : base(detail)
    {
        StatusCode = (int)statusCode;
        Detail = detail;
    }

    public int StatusCode { get; }
    public string Detail { get; }
}

public sealed record GeminiContent(string Role, IEnumerable<GeminiPart> Parts);
public sealed record GeminiPart(string Text);

public sealed class GeminiGenerationConfig
{
    public double Temperature { get; set; }
    public int TopK { get; set; }
    public double TopP { get; set; }
    public int MaxOutputTokens { get; set; }
}
