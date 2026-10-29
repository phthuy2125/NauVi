using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FoodWiseAi.Api.Services;

public class PythonTop3Item
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}

public class PythonPredictResponse
{
    [JsonPropertyName("top3")]
    public List<PythonTop3Item> Top3 { get; set; } = new();

    [JsonPropertyName("model_version")]
    public string ModelVersion { get; set; } = string.Empty;
}

public interface IFoodImagePredictionService
{
    Task<PythonPredictResponse> PredictAsync(
        Stream imageStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);
}

public sealed class FoodImagePredictionService : IFoodImagePredictionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FoodImagePredictionService> _logger;

    public FoodImagePredictionService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<FoodImagePredictionService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var baseUrl = configuration["AiImageService:BaseUrl"] ?? "http://127.0.0.1:8001";
        if (!baseUrl.EndsWith("/")) baseUrl += "/";
        _httpClient.BaseAddress = new Uri(baseUrl);

        var timeoutSeconds = configuration.GetValue<int>("AiImageService:TimeoutSeconds", 10);
        _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds > 0 ? timeoutSeconds : 10);
    }

    public async Task<PythonPredictResponse> PredictAsync(
        Stream imageStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(imageStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType);

        content.Add(streamContent, "file", fileName);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync("predict", content, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Timeout khi gọi dịch vụ Python AI sau {Timeout}s", _httpClient.Timeout.TotalSeconds);
            throw new TimeoutException("Dịch vụ nhận diện AI không phản hồi kịp thời.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi kết nối tới dịch vụ Python AI");
            throw;
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Dịch vụ AI trả về mã lỗi {StatusCode}: {ErrorBody}", response.StatusCode, errorBody);
            throw new HttpRequestException($"Dịch vụ AI trả về lỗi: {response.StatusCode} - {errorBody}", null, response.StatusCode);
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<PythonPredictResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (result == null || result.Top3 == null)
        {
            throw new InvalidOperationException("Dịch vụ AI trả về kết quả rỗng hoặc không đúng định dạng.");
        }

        return result;
    }
}
