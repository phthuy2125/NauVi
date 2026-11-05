using System.Net.Http.Headers;
using System.Net.Http.Json;
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

/// <summary>
/// Dịch vụ kết nối đến AI Service Python (FastAPI) qua HTTP Client.
/// </summary>
public sealed class FoodImagePredictionService : IFoodImagePredictionService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FoodImagePredictionService> _logger;

    public FoodImagePredictionService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<FoodImagePredictionService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<PythonPredictResponse> PredictAsync(
        Stream imageStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        // 1. Đọc URL và cấu hình Timeout từ appsettings
        // Mặc định 45 giây: Gemini Vision cần retry qua nhiều model khi cao tải
        var baseUrl = (_configuration["AiImageService:BaseUrl"] ?? "http://127.0.0.1:8001").TrimEnd('/');
        var timeoutSeconds = _configuration.GetValue<int>("AiImageService:TimeoutSeconds", 45);

        // 2. Thiết lập token hủy khi quá thời gian chờ (Timeout)
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            // 3. Đóng gói file ảnh dưới dạng MultipartFormDataContent
            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(imageStream);

            var validContentType = string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType;
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(validContentType);

            var validFileName = string.IsNullOrWhiteSpace(fileName) ? "food.jpg" : fileName;
            content.Add(streamContent, "file", validFileName);

            // 4. Gửi HTTP POST sang Dịch vụ Python FastAPI (/predict)
            var requestUrl = $"{baseUrl}/predict";
            var response = await _httpClient.PostAsync(requestUrl, content, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cts.Token);
                _logger.LogWarning("Dịch vụ AI Python trả về mã {StatusCode}: {Body}", response.StatusCode, errorBody);
                throw new HttpRequestException($"Dịch vụ AI phản hồi lỗi: {response.StatusCode} - {errorBody}");
            }

            // 5. Đọc kết quả Top 3 và Model Version từ JSON phản hồi
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var result = await response.Content.ReadFromJsonAsync<PythonPredictResponse>(jsonOptions, cts.Token);
            return result ?? new PythonPredictResponse();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Quá thời gian quy định TimeoutSeconds
            _logger.LogError("Yêu cầu nhận diện ảnh tới {BaseUrl} bị quá thời gian ({Timeout}s).", baseUrl, timeoutSeconds);
            throw new TimeoutException($"Dịch vụ AI không phản hồi sau {timeoutSeconds} giây.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Không thể kết nối đến dịch vụ AI tại {BaseUrl}.", baseUrl);
            throw;
        }
    }
}
