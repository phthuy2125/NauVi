using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using FoodWiseAi.Api.Models;

namespace FoodWiseAi.Api.Services;

public class CopilotService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public CopilotService(HttpClient httpClient, IConfiguration configuration, IWebHostEnvironment environment)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _environment = environment;
    }

    public async Task UpdateIngredientsJsonAsync()
    {
        var apiKey = _configuration["COPILOT_API_KEY"] ?? Environment.GetEnvironmentVariable("COPILOT_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.WriteLine("❌ Thiếu COPILOT_API_KEY (GitHub Token) trong appsettings.json hoặc biến môi trường.");
            return;
        }

        var path = Path.Combine(_environment.ContentRootPath, "SeedData", "ingredients.json");
        if (!File.Exists(path))
        {
            Console.WriteLine($"❌ Không tìm thấy file {path}");
            return;
        }

        var json = await File.ReadAllTextAsync(path);
        var ingredients = JsonSerializer.Deserialize<List<Ingredient>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<Ingredient>();

        Console.WriteLine($"🔍 Bắt đầu cập nhật {ingredients.Count} nguyên liệu từ Microsoft Copilot (GPT-4o)...");

        foreach (var ingredient in ingredients)
        {
            int retries = 0;
            bool success = false;
            while (retries < 3 && !success)
            {
                try
                {
                    if (retries == 0) Console.Write($"Đang lấy giá cho '{ingredient.Name}'... ");
                    else Console.Write($"[Retry {retries}] Đang lấy giá cho '{ingredient.Name}'... ");
                    
                    var prompt = $"Hãy cho biết giá bán lẻ thông thường của '{ingredient.Name}' (đơn vị: {ingredient.Unit}) tại các siêu thị Việt Nam (Bách Hóa Xanh và Co.op Mart) tính theo VND. " +
                                 "Chỉ trả về JSON thuần (KHÔNG markdown, KHÔNG giải thích), ví dụ: " +
                                 "{\"bachhoaxanh\": 45000, \"coopmart\": 48000}. Giá là số nguyên VND tương ứng với đúng đơn vị được yêu cầu.";

                    var requestContent = new
                    {
                        messages = new[]
                        {
                            new { role = "user", content = prompt }
                        },
                        model = "gpt-4o-mini",
                        temperature = 0.0
                    };

                    var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://models.inference.ai.azure.com/chat/completions");
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                    requestMessage.Content = JsonContent.Create(requestContent);

                    var response = await _httpClient.SendAsync(requestMessage);
                    if (response.IsSuccessStatusCode)
                    {
                        success = true;
                        var responseString = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(responseString);
                        var choices = doc.RootElement.GetProperty("choices");
                        if (choices.GetArrayLength() > 0)
                        {
                            var text = choices[0].GetProperty("message").GetProperty("content").GetString();
                            
                            var match = Regex.Match(text ?? "", @"\{[\s\S]*\}");
                            if (match.Success)
                            {
                                var priceData = JsonSerializer.Deserialize<Dictionary<string, decimal>>(match.Value, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                if (priceData != null)
                                {
                                    ingredient.CurrentPrice ??= new CurrentPriceInfo();
                                    
                                    if (priceData.TryGetValue("bachhoaxanh", out var bhx) && bhx > 0)
                                    {
                                        ingredient.CurrentPrice.Bachhoaxanh = bhx;
                                    }
                                    if (priceData.TryGetValue("coopmart", out var coop) && coop > 0)
                                    {
                                        ingredient.CurrentPrice.Coopmart = coop;
                                    }
                                    ingredient.CurrentPrice.PriceSource = "Copilot";
                                    ingredient.CurrentPrice.UpdatedAt = DateTime.UtcNow;
                                    Console.WriteLine($"✅ BHX: {ingredient.CurrentPrice.Bachhoaxanh}đ, Coop: {ingredient.CurrentPrice.Coopmart}đ");
                                }
                                else
                                {
                                    Console.WriteLine("⚠️ Không parse được giá.");
                                }
                            }
                            else
                            {
                                Console.WriteLine("⚠️ Copilot không trả về định dạng JSON.");
                            }
                        }
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        Console.WriteLine($"⚠️ Bị giới hạn (Rate Limit). Đang chờ 35 giây để phục hồi...");
                        await Task.Delay(35000); // Thường GitHub bắt chờ 33s
                        retries++;
                        continue;
                    }
                    else
                    {
                        Console.WriteLine($"❌ Lỗi API: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                        success = true; // Break out of retry if it's not a rate limit error
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Lỗi: {ex.Message}");
                    success = true;
                }
            }

            // Delay 8s to respect GitHub Models free limit (gpt-4o-mini allows 15 req/min = 1 req/4s, nhưng ta để 8s cho chắc)
            await Task.Delay(8000);
        }

        var updatedJson = JsonSerializer.Serialize(ingredients, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await File.WriteAllTextAsync(path, updatedJson);
        Console.WriteLine($"✅ Đã lưu {ingredients.Count} nguyên liệu vào {path}.");
    }
}
