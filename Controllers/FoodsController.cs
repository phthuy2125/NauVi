using System.Security.Claims;
using System.Text.Json;
using FoodWiseAi.Api.Data;
using FoodWiseAi.Api.DTOs;
using FoodWiseAi.Api.Models;
using FoodWiseAi.Api.Services;
using FoodWiseAi.Api.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace FoodWiseAi.Api.Controllers;

[ApiController]
[Route("api/foods")]
public sealed class FoodsController : ControllerBase
{
    private readonly MongoDbContext _dbContext;
    private readonly JwtService _jwtService;
    private readonly GeminiService _geminiService;
    private readonly IWebHostEnvironment _environment;
    private readonly IFoodImagePredictionService _predictionService;
    private readonly IConfiguration _configuration;

    public FoodsController(
        MongoDbContext dbContext,
        JwtService jwtService,
        GeminiService geminiService,
        IWebHostEnvironment environment,
        IFoodImagePredictionService predictionService,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _jwtService = jwtService;
        _geminiService = geminiService;
        _environment = environment;
        _predictionService = predictionService;
        _configuration = configuration;
    }
    [Authorize]
    [HttpGet("recommend")]
    public async Task<IActionResult> Recommend()
    {
        try
        {
            var userId = _jwtService.GetUserId(User);
            var user = await _dbContext.Users.Find(x => x.Id == userId).FirstOrDefaultAsync();
            if (user is null)
            {
                return NotFound(new { message = "Không tìm thấy user" });
            }

            var foods = await _dbContext.Foods.Find(Builders<Food>.Filter.Empty).ToListAsync();
            var prefs = user.Preferences;

            // Sắp xếp dựa trên mức độ phù hợp với preferences
            var ranked = foods
                .Select(food => new
                {
                    Food = food,
                    Score =
                        (food.Type != null && food.Type.Equals(prefs.Texture, StringComparison.OrdinalIgnoreCase) ? 30 : 0)
                        + (prefs.Light && food.Calories < 500 ? 20 : 0)
                        + (prefs.Organic && food.Tags != null && food.Tags.Any(t => t.Contains("organic", StringComparison.OrdinalIgnoreCase)) ? 20 : 0)
                        + 10
                })
                .OrderByDescending(x => x.Score)
                .Take(10)
                .Select(x => x.Food)
                .ToList();

            return Ok(new { recommendations = ranked });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Gợi ý món ăn theo khẩu vị cá nhân cho trang chủ.
    /// Không yêu cầu JWT — nhận userId qua query string từ localStorage.
    /// Trả về: featured (món điểm cao nhất) + suggestions (top 3 tiếp theo) + matchPercent.
    /// </summary>
    [HttpGet("recommend-home")]
    public async Task<IActionResult> RecommendHome([FromQuery] string? userId)
    {
        try
        {
            var allFoods = await _dbContext.Foods.Find(x => x.IsAvailable).ToListAsync();

            // Fallback: nếu chưa đăng nhập thì trả về ngẫu nhiên
            if (string.IsNullOrWhiteSpace(userId))
            {
                var random = allFoods.OrderBy(_ => Random.Shared.Next()).Take(4).ToList();
                return Ok(new
                {
                    featured = random.FirstOrDefault(),
                    suggestions = random.Skip(1).ToList(),
                    matchPercent = 0,
                    isPersonalized = false
                });
            }

            var user = await _dbContext.Users.Find(x => x.Id == userId).FirstOrDefaultAsync();
            if (user is null)
            {
                var random = allFoods.OrderBy(_ => Random.Shared.Next()).Take(4).ToList();
                return Ok(new
                {
                    featured = random.FirstOrDefault(),
                    suggestions = random.Skip(1).ToList(),
                    matchPercent = 0,
                    isPersonalized = false
                });
            }

            var prefs = user.Preferences;

            // Hàm tính điểm: mỗi chiều khẩu vị tối đa 20 điểm, cộng thêm các bonus
            // Taste array trong food: "cay", "ngọt", "mặn", "chua", "umami"
            int ScoreFood(Food food)
            {
                int score = 0;
                const int MaxTasteScore = 20;

                // Map taste tag -> preference value (1-5)
                var tasteMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    { "cay", prefs.Spicy },
                    { "spicy", prefs.Spicy },
                    { "ngọt", prefs.Sweet },
                    { "sweet", prefs.Sweet },
                    { "ngot", prefs.Sweet },
                    { "mặn", prefs.Salty },
                    { "salty", prefs.Salty },
                    { "man", prefs.Salty },
                    { "chua", prefs.Sour },
                    { "sour", prefs.Sour },
                    { "umami", prefs.Umami },
                    { "đậm đà", prefs.Umami },
                    { "dam da", prefs.Umami }
                };

                // Tính điểm từng taste tag khớp với food.Taste
                if (food.Taste != null)
                {
                    foreach (var tasteName in food.Taste)
                    {
                        if (tasteMap.TryGetValue(tasteName.Trim(), out var prefValue))
                        {
                            // prefValue 1-5: chuyển thành 0->0, 3->10, 5->20
                            score += (int)Math.Round(prefValue / 5.0 * MaxTasteScore);
                        }
                    }
                }

                // Bonus texture (type khớp)
                if (!string.IsNullOrEmpty(food.Type) &&
                    food.Type.Equals(prefs.Texture, StringComparison.OrdinalIgnoreCase))
                {
                    score += 25;
                }

                // Bonus light (ít calo)
                if (prefs.Light && food.Calories > 0 && food.Calories < 500)
                    score += 15;

                // Bonus organic tag
                if (prefs.Organic && food.Tags != null &&
                    food.Tags.Any(t => t.Contains("organic", StringComparison.OrdinalIgnoreCase)))
                    score += 15;

                // Điểm cơ sở từ rating
                score += (int)(food.Rating * 2);

                return score;
            }

            // Tính điểm tối đa lý thuyết để quy ra %
            // max = 5 taste * 20 + 25 texture + 15 light + 15 organic + 5*2 rating_max = 170
            const int MaxPossibleScore = 170;

            var ranked = allFoods
                .Select(f => new { Food = f, Score = ScoreFood(f) })
                .OrderByDescending(x => x.Score)
                .ToList();

            var featured = ranked.FirstOrDefault()?.Food;
            var featuredScore = ranked.FirstOrDefault()?.Score ?? 0;
            var matchPercent = Math.Min(100, (int)Math.Round(featuredScore / (double)MaxPossibleScore * 100));

            var suggestions = ranked.Skip(1).Take(3).Select(x => x.Food).ToList();

            return Ok(new
            {
                featured,
                suggestions,
                matchPercent,
                isPersonalized = true,
                preferences = new
                {
                    prefs.Spicy,
                    prefs.Sweet,
                    prefs.Salty,
                    prefs.Sour,
                    prefs.Umami,
                    prefs.Texture,
                    prefs.Light,
                    prefs.Organic
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAll([FromQuery] string? userId)
    {
        try
        {
            var foods = await _dbContext.Foods.Find(Builders<Food>.Filter.Empty).ToListAsync();
            
            // Nếu có userId, tính match percentage dựa theo khẩu vị
            if (!string.IsNullOrWhiteSpace(userId))
            {
                var user = await _dbContext.Users.Find(x => x.Id == userId).FirstOrDefaultAsync();
                if (user != null)
                {
                    // Hàm tính điểm khẩu vị cho từng món
                    int ScoreFood(Food food)
                    {
                        int score = 0;
                        const int MaxTasteScore = 20;

                        var tasteMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "cay", user.Preferences.Spicy },
                            { "spicy", user.Preferences.Spicy },
                            { "ngọt", user.Preferences.Sweet },
                            { "sweet", user.Preferences.Sweet },
                            { "ngot", user.Preferences.Sweet },
                            { "mặn", user.Preferences.Salty },
                            { "salty", user.Preferences.Salty },
                            { "man", user.Preferences.Salty },
                            { "chua", user.Preferences.Sour },
                            { "sour", user.Preferences.Sour },
                            { "umami", user.Preferences.Umami },
                            { "đậm đà", user.Preferences.Umami },
                            { "dam da", user.Preferences.Umami }
                        };

                        if (food.Taste != null)
                        {
                            foreach (var tasteName in food.Taste)
                            {
                                if (tasteMap.TryGetValue(tasteName.Trim(), out var prefValue))
                                {
                                    score += (int)Math.Round(prefValue / 5.0 * MaxTasteScore);
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(food.Type) &&
                            food.Type.Equals(user.Preferences.Texture, StringComparison.OrdinalIgnoreCase))
                        {
                            score += 25;
                        }

                        if (user.Preferences.Light && food.Calories > 0 && food.Calories < 500)
                            score += 15;

                        if (user.Preferences.Organic && food.Tags != null &&
                            food.Tags.Any(t => t.Contains("organic", StringComparison.OrdinalIgnoreCase)))
                            score += 15;

                        score += (int)(food.Rating * 2);

                        return score;
                    }

                    const int MaxPossibleScore = 170;
                    
                    // Tính match % cho từng món
                    foods = foods
                        .Select(f => new
                        {
                            food = f,
                            score = ScoreFood(f),
                            matchPercent = Math.Min(100, (int)Math.Round(ScoreFood(f) / (double)MaxPossibleScore * 100))
                        })
                        .OrderByDescending(x => x.score)
                        .Select(x => {
                            x.food.MatchPercent = x.matchPercent;
                            return x.food;
                        })
                        .ToList();
                }
            }

            var result = foods.Select(food => new
            {
                food.Id,
                food.Name,
                food.NameEn,
                food.Description,
                food.Taste,
                food.Type,
                food.Calories,
                food.Ingredients,
                food.Category,
                food.ImageUrl,
                food.Restaurant,
                food.Tags,
                food.BasePrice,
                food.CurrentPrice,
                priceSource = food.CurrentPrice?.PriceSource ?? "GeminiKnowledge",
                food.PriceHistory,
                food.IsAvailable,
                food.Rating,
                food.ReviewCount,
                food.CreatedAt,
                food.UpdatedAt,
                timeStatus = LocalTimeStatusHelper.GetCurrentStatus(),
                food.MatchPercent
            });

            return Ok(new { foods = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        try
        {
            var food = await _dbContext.Foods.Find(x => x.Id == id).FirstOrDefaultAsync();
            if (food is null)
                return NotFound(new { message = "Không tìm thấy món ăn" });

            return Ok(food);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("{id}/comments")]
    public async Task<IActionResult> AddComment(string id, [FromBody] AddCommentRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.UserId) || request.Rating < 1 || request.Rating > 5)
            {
                return BadRequest(new { message = "Dữ liệu không hợp lệ." });
            }

            var user = await _dbContext.Users.Find(x => x.Id == request.UserId).FirstOrDefaultAsync();
            if (user is null)
                return NotFound(new { message = "Không tìm thấy người dùng." });

            var food = await _dbContext.Foods.Find(x => x.Id == id).FirstOrDefaultAsync();
            if (food is null)
                return NotFound(new { message = "Không tìm thấy món ăn." });

            // Check if user has cooked this food
            var hasCooked = user.CookingHistory != null && user.CookingHistory.Any(h => h.FoodId == id);
            if (!hasCooked)
            {
                return BadRequest(new { message = "Bạn cần hoàn thành nấu món này trước khi được đánh giá." });
            }

            // Check if user already reviewed
            if (food.Comments != null && food.Comments.Any(c => c.UserId == user.Id))
            {
                return BadRequest(new { message = "Bạn đã đánh giá món ăn này rồi." });
            }

            var comment = new FoodComment
            {
                UserId = user.Id!,
                Username = user.Username ?? string.Empty,
                AvatarUrl = user.Avatar,
                Rating = request.Rating,
                Content = request.Content ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            food.Comments ??= new List<FoodComment>();
            food.Comments.Add(comment);

            // Update rating
            food.ReviewCount = food.Comments.Count;
            food.Rating = Math.Round((decimal)food.Comments.Average(c => c.Rating), 1);

            var update = Builders<Food>.Update
                .Set(x => x.Comments, food.Comments)
                .Set(x => x.ReviewCount, food.ReviewCount)
                .Set(x => x.Rating, food.Rating)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);

            await _dbContext.Foods.UpdateOneAsync(x => x.Id == id, update);

            return Ok(new { message = "Đã gửi đánh giá thành công", comment, newRating = food.Rating, newCount = food.ReviewCount });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPatch("{id}/prep-image")]
    public async Task<IActionResult> UpdatePrepImage(string id, [FromBody] UpdatePrepImageRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.ImageUrl))
            {
                return BadRequest(new { message = "URL ảnh không hợp lệ." });
            }

            var food = await _dbContext.Foods.Find(x => x.Id == id).FirstOrDefaultAsync();
            if (food is null)
                return NotFound(new { message = "Không tìm thấy món ăn." });

            var update = Builders<Food>.Update
                .Set(x => x.PrepImageUrl, request.ImageUrl)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);

            await _dbContext.Foods.UpdateOneAsync(x => x.Id == id, update);

            return Ok(new { message = "Đã cập nhật ảnh sơ chế", prepImageUrl = request.ImageUrl });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("{id}/prep-image/upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadPrepImage(string id, IFormFile? file)
    {
        try
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { message = "Vui lòng chọn ảnh để tải lên." });

            // Validate extension
            var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExt.Contains(ext))
                return BadRequest(new { message = "Chỉ chấp nhận ảnh định dạng jpg, png, webp, gif." });

            // Validate size (max 5MB)
            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(new { message = "Kích thước ảnh tối đa là 5MB." });

            var food = await _dbContext.Foods.Find(x => x.Id == id).FirstOrDefaultAsync();
            if (food is null)
                return NotFound(new { message = "Không tìm thấy món ăn." });

            // Save file to uploads/
            var uploadsDir = Path.Combine(_environment.ContentRootPath, "uploads");
            Directory.CreateDirectory(uploadsDir);
            var fileName = $"prep-{id}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            await using (var stream = System.IO.File.Create(filePath))
            {
                await file.CopyToAsync(stream);
            }

            var imageUrl = $"{Request.Scheme}://{Request.Host}/uploads/{fileName}";

            // Persist URL to MongoDB
            var update = Builders<Food>.Update
                .Set(x => x.PrepImageUrl, imageUrl)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);

            await _dbContext.Foods.UpdateOneAsync(x => x.Id == id, update);

            return Ok(new { message = "Đã tải ảnh sơ chế lên thành công", prepImageUrl = imageUrl });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("realtime-prices")]
    public async Task RealtimePrices(CancellationToken cancellationToken)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var foods = await _dbContext.Foods.Find(x => x.IsAvailable).ToListAsync(cancellationToken);
                var payload = foods.Select(food => new
                {
                    id = food.Id,
                    coopmartPrice = food.CurrentPrice?.Coopmart ?? 0,
                    bachhoaxanhPrice = food.CurrentPrice?.Bachhoaxanh ?? 0,
                    status = LocalTimeStatusHelper.GetCurrentStatus(),
                    currentHour = DateTime.Now.Hour
                });

                await Response.WriteAsync($"data: {JsonSerializer.Serialize(payload)}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi khi lấy dữ liệu giá thời gian thực: " + ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        }
    }

    [Authorize]
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create([FromForm] FoodCreateForm form)
    {
        try
        {
            var userId = _jwtService.GetUserId(User);
            var user = await _dbContext.Users.Find(x => x.Id == userId).FirstOrDefaultAsync();
            if (user is null || !string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(403, new { message = "Chỉ dành cho Admin" });
            }

            var imageUrl = form.ImageUrl ?? string.Empty;
            if (form.ImageFile is not null)
            {
                var uploadsDir = Path.Combine(_environment.ContentRootPath, "uploads");
                Directory.CreateDirectory(uploadsDir);
                var fileName = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Path.GetFileName(form.ImageFile.FileName)}";
                var filePath = Path.Combine(uploadsDir, fileName);

                await using var stream = System.IO.File.Create(filePath);
                await form.ImageFile.CopyToAsync(stream);
                imageUrl = $"{Request.Scheme}://{Request.Host}/uploads/{fileName}";
            }

            var newFood = new Food
            {
                Name = form.Name ?? string.Empty,
                NameEn = form.NameEn,
                Description = form.Description,
                Taste = ParseArray(form.Taste),
                Type = form.Type,
                Calories = int.TryParse(form.Calories, out var calories) ? calories : 0,
                Ingredients = ParseArray(form.Ingredients),
                Category = form.Category,
                Restaurant = form.Restaurant,
                Tags = ParseArray(form.Tags),
                ImageUrl = imageUrl,
                BasePrice = new PricePair
                {
                    Coopmart = decimal.TryParse(form.BasePriceCoop, out var coop) ? coop : 0,
                    Bachhoaxanh = decimal.TryParse(form.BasePriceBHX, out var bhx) ? bhx : 0
                },
                CurrentPrice = new CurrentPriceInfo
                {
                    Coopmart = decimal.TryParse(form.BasePriceCoop, out var currentCoop) ? currentCoop : 0,
                    Bachhoaxanh = decimal.TryParse(form.BasePriceBHX, out var currentBhx) ? currentBhx : 0,
                    UpdatedAt = DateTime.UtcNow
                },
                Rating = 5.0m,
                ReviewCount = 0,
                IsAvailable = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _dbContext.Foods.InsertOneAsync(newFood);
            return StatusCode(201, new { message = "Đã thêm món ăn thành công", food = newFood });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("ai-match")]
    public async Task<IActionResult> AiMatch([FromBody] FoodAiMatchRequest request)
    {
        try
        {
            if (!_geminiService.HasApiKey)
            {
                return StatusCode(500, new { error = "Chưa cấu hình API Key cho Gemini!" });
            }

            var allFoods = await _dbContext.Foods.Find(x => x.IsAvailable).ToListAsync();
            var criteria = request.Criteria ?? new FoodCriteria();

            var prompt = $$"""
Bạn là một chuyên gia ẩm thực. Dưới đây là danh sách các món ăn có trong menu:
{{JsonSerializer.Serialize(allFoods.Select(food => new { food.Id, food.Name, food.Taste, food.Type, food.Ingredients, food.Description }))}}

Người dùng đang tìm món ăn với tiêu chí:
- Nguyên liệu: {{string.Join(", ", criteria.Ingredients ?? new List<string> { "Bất kỳ" })}}
- Khẩu vị: {{string.Join(", ", criteria.Taste ?? new List<string> { "Bất kỳ" })}}
- Chế độ ăn: {{criteria.Diet ?? "Bình thường"}}
- Kết cấu: {{criteria.Consistency ?? "Bất kỳ"}}
- Tinh bột: {{criteria.Carbs ?? "Bất kỳ"}}

Nhiệm vụ:
1. Phân tích danh sách món ăn trên.
2. Chọn tối đa 6 món khớp nhất với yêu cầu, ưu tiên nguyên liệu và khẩu vị đúng.
3. Trả về ĐÚNG mảng JSON chứa _id hoặc Id của các món. Ví dụ: ["id1", "id2", "id3"]
KHÔNG giải thích thêm, chỉ trả về mảng JSON.
""";

            var result = await _geminiService.GenerateTextAsync("gemini-2.0-flash", prompt);
            var matchedIds = JsonBlockExtractor.ExtractArray<List<string>>(result);
            var foods = await _dbContext.Foods.Find(x => matchedIds.Contains(x.Id!)).ToListAsync();

            return Ok(new { matches = foods });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Lỗi xử lý AI: " + ex.Message });
        }
    }

    [HttpGet("ingredients")]
    public async Task<IActionResult> GetIngredients()
    {
        try
        {
            var ingredients = await _dbContext.Ingredients.Find(x => x.IsAvailable).ToListAsync();
            return Ok(new { ingredients });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("suggest-by-ingredients")]
    public async Task<IActionResult> SuggestByIngredients([FromBody] SuggestByIngredientsRequest request)
    {
        try
        {
            var names = request.SelectedIngredientNames ?? new List<string>();
            if (names.Count == 0)
            {
                return BadRequest(new { message = "Vui lòng chọn ít nhất một nguyên liệu!" });
            }

            var ingredients = await _dbContext.Ingredients.Find(x => names.Contains(x.Name)).ToListAsync();
            var totalCoop = ingredients.Sum(x => x.CurrentPrice?.Coopmart ?? 0);
            var totalBhx = ingredients.Sum(x => x.CurrentPrice?.Bachhoaxanh ?? 0);

            var foods = await _dbContext.Foods.Find(Builders<Food>.Filter.Empty).ToListAsync();
            var matches = foods
                .Select(food => new
                {
                    Food = food,
                    MatchCount = food.Ingredients.Intersect(names, StringComparer.OrdinalIgnoreCase).Count()
                })
                .Where(x => x.MatchCount > 0)
                .OrderByDescending(x => x.MatchCount)
                .ThenByDescending(x => x.Food.Rating)
                .Take(10)
                .Select(x => x.Food)
                .ToList();

            return Ok(new
            {
                totalPrice = new { coopmart = totalCoop, bachhoaxanh = totalBhx },
                selectedIngredients = ingredients,
                suggestedFoods = matches
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private static List<string> ParseArray(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    // =====================================================================
    // ENDPOINT CHO OPENCLAW: Nhận dữ liệu giá scrape từ BHX / Coopmart
    // POST /api/prices/update-scraped
    // Header: X-Scraper-Key: <giá trị trong appsettings.json>
    // Body: danh sách ScrapedPriceItem
    // =====================================================================
    [HttpPost("/api/prices/update-scraped")]
    public async Task<IActionResult> UpdateScrapedPrices(
        [FromHeader(Name = "X-Scraper-Key")] string? scraperKey,
        [FromBody] List<ScrapedPriceItem>? items)
    {
        // Xác thực bằng scraper key đơn giản (không cần JWT)
        var expectedKey = _dbContext.GetType().Assembly
            .GetType("Microsoft.Extensions.Configuration.IConfiguration") is null
            ? "foodwise-scraper-key"
            : "foodwise-scraper-key";

        // Đọc key từ config thông qua HttpContext
            var cfg = HttpContext.RequestServices.GetService<IConfiguration>();
            var configKey = cfg?["NauVi:ScraperApiKey"] ?? cfg?["FoodWise:ScraperApiKey"] ?? "nauvi-scraper-key";

        if (string.IsNullOrWhiteSpace(scraperKey) || scraperKey != configKey)
        {
            return Unauthorized(new { message = "Scraper key không hợp lệ. Thêm header: X-Scraper-Key" });
        }

        if (items is null || items.Count == 0)
        {
            return BadRequest(new { message = "Không có dữ liệu giá nào được gửi lên." });
        }

        var updatedIngredients = 0;
        var updatedFoods = 0;
        var skipped = new List<string>();
        var utcNow = DateTime.UtcNow;

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Name)) continue;

            // Tìm trong Ingredients trước
            var ingredient = await _dbContext.Ingredients
                .Find(x => x.Name.ToLower() == item.Name.ToLower())
                .FirstOrDefaultAsync();

            if (ingredient is not null)
            {
                var update = Builders<Ingredient>.Update
                    .Set(x => x.CurrentPrice.UpdatedAt, utcNow)
                    .Set(x => x.UpdatedAt, utcNow);

                if (item.Store?.ToLower().Contains("coopmart") == true && item.Price > 0)
                    update = update.Set(x => x.CurrentPrice.Coopmart, item.Price);
                else if (item.Store?.ToLower().Contains("bach") == true && item.Price > 0)
                    update = update.Set(x => x.CurrentPrice.Bachhoaxanh, item.Price);
                else
                {
                    // Cập nhật cả hai nếu không rõ cửa hàng
                    if (item.Price > 0)
                        update = update
                            .Set(x => x.CurrentPrice.Coopmart, item.Price)
                            .Set(x => x.CurrentPrice.Bachhoaxanh, item.Price);
                }

                await _dbContext.Ingredients.UpdateOneAsync(x => x.Id == ingredient.Id, update);
                updatedIngredients++;
                continue;
            }

            // Nếu không tìm thấy trong Ingredients, tìm trong Foods
            var food = await _dbContext.Foods
                .Find(x => x.Name.ToLower() == item.Name.ToLower())
                .FirstOrDefaultAsync();

            if (food is not null)
            {
                var update = Builders<Food>.Update
                    .Set(x => x.CurrentPrice.UpdatedAt, utcNow)
                    .Set(x => x.UpdatedAt, utcNow);

                if (item.Store?.ToLower().Contains("coopmart") == true && item.Price > 0)
                    update = update.Set(x => x.CurrentPrice.Coopmart, item.Price);
                else if (item.Store?.ToLower().Contains("bach") == true && item.Price > 0)
                    update = update.Set(x => x.CurrentPrice.Bachhoaxanh, item.Price);
                else if (item.Price > 0)
                    update = update
                        .Set(x => x.CurrentPrice.Coopmart, item.Price)
                        .Set(x => x.CurrentPrice.Bachhoaxanh, item.Price);

                await _dbContext.Foods.UpdateOneAsync(x => x.Id == food.Id, update);
                updatedFoods++;
                continue;
            }

            skipped.Add(item.Name);
        }

        return Ok(new
        {
            message = $"Cập nhật thành công {updatedIngredients} nguyên liệu, {updatedFoods} món ăn.",
            updatedIngredients,
            updatedFoods,
            skipped,
            updatedAt = utcNow
        });
    }

    // =====================================================================
    // ENDPOINT HỖ TRỢ OPENCLAW: Lấy danh sách tên nguyên liệu có trong DB
    // GET /api/prices/ingredient-names
    // Dùng để OpenClaw biết cần tìm kiếm giá của những nguyên liệu nào
    // =====================================================================
    [HttpGet("/api/prices/ingredient-names")]
    public async Task<IActionResult> GetIngredientNames()
    {
        try
        {
            var ingredients = await _dbContext.Ingredients
                .Find(x => x.IsAvailable)
                .ToListAsync();

            var result = ingredients.Select(i => new
            {
                name = i.Name,
                category = i.Category,
                currentCoopmart = i.CurrentPrice?.Coopmart ?? 0,
                currentBHX = i.CurrentPrice?.Bachhoaxanh ?? 0,
                lastUpdated = i.CurrentPrice?.UpdatedAt
            });

            return Ok(new
            {
                count = result.Count(),
                ingredients = result,
                hint = "Dùng danh sách này để OpenClaw tìm kiếm giá trên BachHoaXanh và Coopmart"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Nhận diện món ăn từ hình ảnh tải lên và gợi ý top-3 món ăn kèm độ tin cậy.
    /// </summary>
    [HttpPost("predict-image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> PredictImage(IFormFile? image)
    {
        // 1. Kiểm tra file không rỗng
        if (image == null || image.Length == 0)
        {
            return BadRequest(new { message = "Vui lòng chọn ảnh món ăn để nhận diện." });
        }

        // 2. Kiểm tra kích thước tối đa 5MB
        const long maxSizeBytes = 5 * 1024 * 1024;
        if (image.Length > maxSizeBytes)
        {
            return BadRequest(new { message = "Kích thước ảnh tối đa là 5MB. Vui lòng chọn ảnh có dung lượng nhỏ hơn." });
        }

        // 3. Kiểm tra ContentType bắt đầu bằng "image/"
        if (string.IsNullOrWhiteSpace(image.ContentType) || !image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Tập tin tải lên phải là hình ảnh (ContentType image/*)." });
        }

        // 4. Kiểm tra định dạng đuôi file: jpg, jpeg, png, webp
        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(new { message = "Hệ thống chỉ hỗ trợ định dạng ảnh JPG, JPEG, PNG hoặc WEBP." });
        }

        // 5. Gọi dịch vụ Python AI
        PythonPredictResponse aiResult;
        try
        {
            using var stream = image.OpenReadStream();
            aiResult = await _predictionService.PredictAsync(
                stream,
                image.FileName,
                image.ContentType,
                HttpContext.RequestAborted);
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is TimeoutException)
        {
            return StatusCode(503, new
            {
                message = "Dịch vụ AI nhận diện ảnh hiện đang không khả dụng hoặc phản hồi quá thời gian cho phép. Vui lòng kiểm tra dịch vụ AI hoặc thử lại sau."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = $"Đã xảy ra lỗi trong quá trình xử lý ảnh: {ex.Message}"
            });
        }

        // 6. Ánh xạ nhãn trả về sang món trong collection foods (không phân biệt hoa thường và dấu)
        var allFoods = await _dbContext.Foods.Find(Builders<Food>.Filter.Empty).ToListAsync();
        var top3Results = new List<object>();
        var top3LogItems = new List<PredictionFoodItem>();

        foreach (var item in aiResult.Top3.Take(3))
        {
            var matchedFood = VietnameseTextNormalizer.FindMatchingFood(item.Label, allFoods);
            var foodId = matchedFood?.Id;
            var displayName = matchedFood != null ? matchedFood.Name : item.Label;

            top3Results.Add(new
            {
                name = displayName,
                confidence = Math.Round(item.Confidence, 4),
                foodId = foodId
            });

            top3LogItems.Add(new PredictionFoodItem
            {
                Name = displayName,
                Confidence = Math.Round(item.Confidence, 4),
                FoodId = foodId
            });
        }

        // 7. Kiểm tra ngưỡng tin cậy (uncertain)
        var uncertainThreshold = _configuration.GetValue<double>("AiImageService:UncertainThreshold", 0.5);
        var top1Confidence = aiResult.Top3.FirstOrDefault()?.Confidence ?? 0.0;
        var isUncertain = top1Confidence < uncertainThreshold;

        // 8. Ghi log mỗi lần dự đoán vào collection prediction_logs
        try
        {
            var userId = _jwtService.GetUserId(User);
            var log = new PredictionLog
            {
                UserId = userId,
                FileName = image.FileName,
                ImageUrl = null, // Không lưu ảnh nếu chưa cần
                Top3 = top3LogItems,
                Confidence = Math.Round(top1Confidence, 4),
                ModelVersion = aiResult.ModelVersion,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _dbContext.PredictionLogs.InsertOneAsync(log);
        }
        catch
        {
            // Không làm gián đoạn response nếu ghi log thất bại
        }

        // 9. Trả kết quả response theo yêu cầu
        return Ok(new
        {
            top3 = top3Results,
            uncertain = isUncertain
        });
    }
}

public class AddCommentRequest
{
    public string UserId { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Content { get; set; } = string.Empty;
}

public class UpdatePrepImageRequest
{
    public string ImageUrl { get; set; } = string.Empty;
}

/// <summary>
/// Dữ liệu giá một sản phẩm do OpenClaw scrape từ web.
/// </summary>
public class ScrapedPriceItem
{
    /// <summary>Tên sản phẩm (phải khớp tên trong DB)</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Giá (VND)</summary>
    public decimal Price { get; set; }

    /// <summary>Tên cửa hàng: "coopmart" hoặc "bachhoaxanh"</summary>
    public string? Store { get; set; }

    /// <summary>Đơn vị tính (tùy chọn)</summary>
    public string? Unit { get; set; }

    /// <summary>URL nguồn đã scrape (tùy chọn, để debug)</summary>
    public string? SourceUrl { get; set; }
}
