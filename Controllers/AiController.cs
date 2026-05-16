using System.Text.Json.Serialization;
using FoodWiseAi.Api.DTOs;
using FoodWiseAi.Api.Models;
using FoodWiseAi.Api.Services;
using FoodWiseAi.Api.Utilities;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace FoodWiseAi.Api.Controllers;

[ApiController]
[Route("api/ai")]
public sealed class AiController : ControllerBase
{
    private readonly GeminiService _geminiService;
    private readonly FoodWiseAi.Api.Data.MongoDbContext _dbContext;

    private static readonly string[] ModelsToTry =
    {
        "gemini-2.0-flash",
        "gemini-2.5-flash",
        "gemini-2.0-flash-lite"
    };

    private string GetSystemPrompt(IEnumerable<Food> dbFoods, int dailyCalories = 0)
    {
        // Gửi danh sách món ăn đầy đủ hơn (tối đa 100 món để không quá token)
        var foodEntries = dbFoods
            .Take(100)
            .Select(f => $"{f.Name}|{f.Id}");
        var foodListText = string.Join("\n", foodEntries);

        var healthNote = "";
        if (dailyCalories > 3000)
        {
            healthNote = $"\n🚨 CẢNH BÁO: Hôm nay người dùng đã nạp {dailyCalories} kcal (Vượt mức 3000 kcal/ngày). " +
                         "Hãy đưa ra lời khuyên hạn chế ăn thêm, chọn món cực nhẹ (salad, nước lọc) và khuyên họ nghỉ ngơi/vận động.";
        }
        else if (dailyCalories > 0)
        {
            healthNote = $"\n📊 Chỉ số: Hôm nay người dùng đã nạp {dailyCalories} kcal.";
        }

        return $"""
Bạn là FoodWise AI ⚡ - Trợ lý ẩm thực của hệ thống FoodWise.
Dưới đây là DANH SÁCH MÓN ĂN TRONG HỆ THỐNG:
{foodListText}
{healthNote}

QUY TẮC QUAN TRỌNG:
1️⃣ LUÔN LUÔN ưu tiên giới thiệu các món có trong danh sách trên.
2️⃣ Khi nhắc đến bất kỳ món nào trong danh sách trên, bạn PHẢI dùng định dạng: [Tên món](id:ID_MÓN).
3️⃣ Nếu không tìm thấy món chính xác, hãy gợi ý món tương tự có trong hệ thống.
4️⃣ Nếu có cảnh báo sức khỏe (calo cao), hãy lồng ghép lời khuyên vào câu trả lời một cách tự nhiên.
5️⃣ Trả lời ngắn gọn, thân thiện bằng tiếng Việt và dùng emoji.
""";
    }

    public AiController(GeminiService geminiService, FoodWiseAi.Api.Data.MongoDbContext dbContext)
    {
        _geminiService = geminiService;
        _dbContext = dbContext;
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        try
        {
            if (!_geminiService.HasApiKey)
            {
                return StatusCode(500, new
                {
                    error = "Chưa cấu hình API Key cho Gemini!",
                    debug = "Vui lòng kiểm tra biến môi trường GEMINI_API_KEY hoặc appsettings."
                });
            }

            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { error = "Thiếu nội dung tin nhắn!" });
            }

            var contents = new List<GeminiContent>();
            // Giới hạn history: chỉ giữ lại 4 tin nhắn gần nhất để tiết kiệm token
            var history = (request.History ?? new List<ChatHistoryItem>()).TakeLast(4).ToList();
            foreach (var item in history)
            {
                var role = string.Equals(item.Role, "user", StringComparison.OrdinalIgnoreCase) ? "user" : "model";
                contents.Add(new GeminiContent(role, new[] { new GeminiPart(item.Text ?? string.Empty) }));
            }
            contents.Add(new GeminiContent("user", new[] { new GeminiPart(request.Message) }));

            // Lấy danh sách món từ DB
            var dbFoods = await _dbContext.Foods.Find(Builders<Food>.Filter.Empty).ToListAsync();
            
            // Tính toán calo trong NGÀY nếu có UserId
            int dailyCalories = 0;
            if (!string.IsNullOrEmpty(request.UserId))
            {
                var user = await _dbContext.Users.Find(u => u.Id == request.UserId).FirstOrDefaultAsync();
                if (user != null && user.CookingHistory != null)
                {
                    var today = DateTime.UtcNow.Date;
                    var todayItems = user.CookingHistory.Where(h => h.CompletedAt >= today).ToList();
                    foreach (var h in todayItems)
                    {
                        var food = dbFoods.FirstOrDefault(f => f.Id == h.FoodId);
                        if (food != null) dailyCalories += food.Calories;
                    }
                }
            }

            // LUÔN LUÔN gửi system prompt để AI giữ đúng vai trò và quy tắc
            var systemPrompt = GetSystemPrompt(dbFoods, dailyCalories);

            var errors = new List<string>();
            foreach (var modelName in ModelsToTry)
            {
                try
                {
                    var text = await _geminiService.GenerateTextAsync(
                        modelName,
                        contents,
                        systemPrompt,
                        new GeminiGenerationConfig
                        {
                            Temperature = 0.1,
                            TopK = 40,
                            TopP = 0.95,
                            MaxOutputTokens = 2048
                        });

                    // Nếu AI không sử dụng định dạng link cho những món đã có trong DB,
                    // tự động chèn link theo format [Tên món](id:ID_MÓN)
                    try
                    {
                        var reply = text ?? string.Empty;
                        foreach (var f in dbFoods.OrderByDescending(x => x.Name?.Length ?? 0))
                        {
                            if (string.IsNullOrWhiteSpace(f.Name) || string.IsNullOrWhiteSpace(f.Id)) continue;
                            
                            // Nếu reply đã chứa id link của món này thì bỏ qua
                            if (reply.Contains($"(id:{f.Id})")) continue;

                            // Regex cải tiến để khớp tên món ăn tiếng Việt (không dùng \b vì nó không hoạt động tốt với Unicode)
                            // Sử dụng Lookbehind và Lookahead để đảm bảo không khớp giữa từ
                            var escapedName = System.Text.RegularExpressions.Regex.Escape(f.Name);
                            var pattern = $"(?<![\\w\\dÀ-ỹ]){escapedName}(?![\\w\\dÀ-ỹ])";
                            
                            reply = System.Text.RegularExpressions.Regex.Replace(reply, pattern, m => $"[{m.Value}](id:{f.Id})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        }

                        return Ok(new { reply });
                    }
                    catch
                    {
                        return Ok(new { reply = text });
                    }
                }
                catch (GeminiApiException ex)
                {
                    errors.Add($"{modelName}: {ex.StatusCode} {ex.Detail}");
                }
                catch (Exception ex)
                {
                    errors.Add($"{modelName}: {ex.Message}");
                }
            }

            return StatusCode(500, new
            {
                error = BuildErrorMessage(errors),
                detail = string.Join(" | ", errors)
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Lỗi hệ thống khi xử lý AI.", detail = ex.Message });
        }
    }

    [HttpPost("suggest-from-ingredients")]
    public async Task<IActionResult> SuggestFromIngredients([FromBody] SuggestFromIngredientsRequest request)
    {
        try
        {
            if (!_geminiService.HasApiKey)
            {
                return StatusCode(500, new { error = "Chưa cấu hình GEMINI_API_KEY trong appsettings hoặc biến môi trường!" });
            }

            var selectedIngredients = request.SelectedIngredients ?? new List<string>();
            if (selectedIngredients.Count == 0)
            {
                return BadRequest(new { error = "Vui lòng cung cấp danh sách nguyên liệu!" });
            }

            var dbFoods = await _dbContext.Foods.Find(MongoDB.Driver.Builders<Food>.Filter.Empty).ToListAsync();
            var dbFoodContext = string.Join("\n", dbFoods.Select(f => $"{f.Name} | ID:{f.Id} | NL:{string.Join(",", f.Ingredients ?? new List<string>())}"));

            var ingredientList = string.Join(", ", selectedIngredients);
            var prompt = $$"""
Bạn là một siêu đầu bếp và chuyên gia dinh dưỡng của FoodWise.
Dưới đây là DANH SÁCH MÓN ĂN CHÍNH THỨC của chúng tôi:
{{dbFoodContext}}

Yêu cầu:
1. Gợi ý 3 món ăn từ nguyên liệu: {{ingredientList}}
2. ƯU TIÊN chọn món từ DANH SÁCH CHÍNH THỨC ở trên.
3. Nếu chọn món từ danh sách, PHẢI điền ID vào "db_id". Nếu món mới, "db_id" là null.
4. Chỉ trả về JSON, không giải thích.

JSON mẫu:
{
  "mon_an_goi_y": [
    {
      "db_id": "ID_MÓN",
      "name": "Tên món",
      "description": "Mô tả",
      ...
    }
}
""";

            var errors = new List<string>();
            foreach (var modelName in ModelsToTry)
            {
                try
                {
                    var text = await _geminiService.GenerateTextAsync(
                        modelName,
                        prompt,
                        generationConfig: new GeminiGenerationConfig
                        {
                            Temperature = 0.2,
                            TopK = 40,
                            TopP = 0.95,
                            MaxOutputTokens = 3000
                        });

                    var parsed = JsonBlockExtractor.ExtractObject<AiSuggestionResponse>(text);
                    return Ok(new
                    {
                        success = true,
                        selectedIngredients,
                        suggestions = parsed.MonAnGoiY ?? new List<AiSuggestedFood>()
                    });
                }
                catch (GeminiApiException ex)
                {
                    errors.Add($"{modelName}: {ex.StatusCode} {ex.Detail}");
                }
                catch (Exception ex)
                {
                    errors.Add($"{modelName}: {ex.Message}");
                }
            }

            return StatusCode(500, new
            {
                error = BuildErrorMessage(errors),
                detail = errors
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Lỗi hệ thống: " + ex.Message });
        }
    }

    private static string BuildErrorMessage(IEnumerable<string> detailErrors)
    {
        var list = detailErrors.ToList();
        if (list.Any(x => x.Contains("429", StringComparison.OrdinalIgnoreCase)))
        {
            return "🔴 API Key đã hết lượt miễn phí (lỗi 429). Vui lòng tạo API Key mới tại aistudio.google.com và cập nhật lại cấu hình.";
        }
        if (list.Any(x => x.Contains("503", StringComparison.OrdinalIgnoreCase)))
        {
            return "Server AI của Google đang quá tải (lỗi 503). Hãy thử lại sau ít phút.";
        }
        if (list.Any(x => x.Contains("404", StringComparison.OrdinalIgnoreCase)))
        {
            return "API Key không được phân quyền hoặc khu vực không hỗ trợ model này. (" + string.Join(" | ", list) + ")";
        }
        return "Lỗi xử lý AI. Chi tiết: " + string.Join(" | ", list);
    }

    public sealed class AiSuggestionResponse
    {
        [JsonPropertyName("mon_an_goi_y")]
        public List<AiSuggestedFood>? MonAnGoiY { get; set; }
    }

    public sealed class AiSuggestedFood
    {
        [JsonPropertyName("db_id")]
        public string? DbId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("nameEn")]
        public string? NameEn { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("taste")]
        public List<string>? Taste { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("calories")]
        public int Calories { get; set; }

        [JsonPropertyName("ingredients")]
        public List<string>? Ingredients { get; set; }

        [JsonPropertyName("nguyen_lieu_can_them")]
        public List<string>? NguyenLieuCanThem { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        [JsonPropertyName("basePrice")]
        public object? BasePrice { get; set; }

        [JsonPropertyName("huong_dan_nau")]
        public List<string>? HuongDanNau { get; set; }

        [JsonPropertyName("ly_do_goi_y")]
        public string? LyDoGoiY { get; set; }
    }
}
