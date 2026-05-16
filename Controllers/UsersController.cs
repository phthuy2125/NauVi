using FoodWiseAi.Api.Data;
using FoodWiseAi.Api.DTOs;
using FoodWiseAi.Api.Models;
using FoodWiseAi.Api.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace FoodWiseAi.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly MongoDbContext _dbContext;
    private readonly GoogleAuthService _googleAuthService;
    private readonly JwtService _jwtService;
    private readonly IWebHostEnvironment _environment;

    public UsersController(
        MongoDbContext dbContext,
        GoogleAuthService googleAuthService,
        JwtService jwtService,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _googleAuthService = googleAuthService;
        _jwtService = jwtService;
        _environment = environment;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            if (request.IsGoogle)
            {
                var payload = await _googleAuthService.VerifyAsync(request.GoogleToken ?? string.Empty);
                var existingGoogle = await _dbContext.Users.Find(x => x.GoogleId == payload.Subject).FirstOrDefaultAsync();
                if (existingGoogle is not null)
                {
                    return BadRequest(new { message = "Tài khoản Google này đã được đăng ký!" });
                }

                var user = new User
                {
                    Username = payload.Name,
                    Password = "google_login",
                    Email = payload.Email,
                    GoogleId = payload.Subject,
                    Preferences = request.Preferences ?? new UserPreferences(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _dbContext.Users.InsertOneAsync(user);
                var token = _jwtService.CreateToken(user);

                return Ok(new
                {
                    message = "Đăng ký Google thành công",
                    user,
                    token,
                    userId = user.Id,
                    username = user.Username
                });
            }

            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Thiếu tên đăng nhập hoặc mật khẩu!" });
            }

            var existing = await _dbContext.Users.Find(x => x.Username == request.Username).FirstOrDefaultAsync();
            if (existing is not null)
            {
                return BadRequest(new { message = "Tên đăng nhập đã tồn tại!" });
            }

            var newUser = new User
            {
                Username = request.Username,
                Password = request.Password,
                Preferences = request.Preferences ?? new UserPreferences(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _dbContext.Users.InsertOneAsync(newUser);
            var jwt = _jwtService.CreateToken(newUser);

            return Ok(new
            {
                message = "Đăng ký thành công",
                user = newUser,
                token = jwt,
                userId = newUser.Id,
                username = newUser.Username
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi server: " + ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Vui lòng nhập đủ thông tin!" });
            }

            var user = await _dbContext.Users.Find(x => x.Username == request.Username && x.Password == request.Password).FirstOrDefaultAsync();
            if (user is null)
            {
                return BadRequest(new { message = "Sai tài khoản hoặc mật khẩu!" });
            }

            var token = _jwtService.CreateToken(user);
            return Ok(new
            {
                message = "Đăng nhập thành công",
                userId = user.Id,
                username = user.Username,
                user,
                token
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi server: " + ex.Message });
        }
    }

    [HttpPost("google-login")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        try
        {
            var payload = await _googleAuthService.VerifyAsync(request.Token ?? string.Empty);
            var user = await _dbContext.Users.Find(x => x.GoogleId == payload.Subject).FirstOrDefaultAsync();
            if (user is null)
            {
                return BadRequest(new { message = "Tài khoản Google này chưa đăng ký!" });
            }

            var token = _jwtService.CreateToken(user);
            return Ok(new
            {
                message = "Đăng nhập Google thành công",
                userId = user.Id,
                username = user.Username,
                token
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi server: " + ex.Message });
        }
    }
    
    [HttpGet("profile/{userId}")]
    public async Task<IActionResult> Profile([FromRoute] string userId)
    {
        try
        {
            var user = await _dbContext.Users.Find(x => x.Id == userId).FirstOrDefaultAsync();
            if (user == null)
                return NotFound(new { message = "Không tìm thấy tài khoản!" });

            return Ok(new
            {
                username = user.Username,
                preferences = user.Preferences,
                avatar = user.Avatar
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi server: " + ex.Message });
        }
    }

    [HttpPut("update-preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferencesRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.UserId) || request.Preferences == null)
                return BadRequest(new { message = "Thiếu thông tin!" });

            var filter = Builders<User>.Filter.Eq(x => x.Id, request.UserId);
            var update = Builders<User>.Update
                .Set(x => x.Preferences, request.Preferences)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);

            var options = new FindOneAndUpdateOptions<User, User>
            {
                ReturnDocument = ReturnDocument.After
            };

            var user = await _dbContext.Users.FindOneAndUpdateAsync(filter, update, options);

            if (user == null)
                return NotFound(new { message = "Không tìm thấy tài khoản!" });

            return Ok(new { message = "Cập nhật khẩu vị thành công!", preferences = user.Preferences });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi server: " + ex.Message });
        }
    }

    [HttpPut("update-password")]
    public async Task<IActionResult> UpdatePassword([FromBody] UpdatePasswordRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { message = "Thiếu thông tin cần thiết!" });
            }

            if (request.NewPassword.Length < 6)
            {
                return BadRequest(new { message = "Mật khẩu mới phải có ít nhất 6 ký tự!" });
            }

            var user = await _dbContext.Users.Find(x => x.Id == request.UserId).FirstOrDefaultAsync();
            if (user is null)
            {
                return NotFound(new { message = "Không tìm thấy tài khoản!" });
            }

            if (string.IsNullOrWhiteSpace(user.GoogleId))
            {
                if (string.IsNullOrWhiteSpace(request.CurrentPassword))
                {
                    return BadRequest(new { message = "Vui lòng nhập mật khẩu hiện tại!" });
                }

                if (!string.Equals(user.Password, request.CurrentPassword, StringComparison.Ordinal))
                {
                    return BadRequest(new { message = "Mật khẩu hiện tại không đúng!" });
                }
            }

            var update = Builders<User>.Update
                .Set(x => x.Password, request.NewPassword)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);

            await _dbContext.Users.UpdateOneAsync(x => x.Id == request.UserId, update);
            return Ok(new { message = "Đổi mật khẩu thành công!" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi server: " + ex.Message });
        }
    }

    [HttpPost("update-avatar")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UpdateAvatar([FromForm] UpdateAvatarRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.UserId) || request.Avatar is null)
            {
                return BadRequest(new { message = "Thiếu thông tin người dùng hoặc file ảnh!" });
            }

            var uploadDir = Path.Combine(_environment.ContentRootPath, "uploads");
            Directory.CreateDirectory(uploadDir);

            var extension = Path.GetExtension(request.Avatar.FileName);
            var fileName = $"avatar-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Random.Shared.Next(100000000, 999999999)}{extension}";
            var filePath = Path.Combine(uploadDir, fileName);

            await using (var stream = System.IO.File.Create(filePath))
            {
                await request.Avatar.CopyToAsync(stream);
            }

            var avatarUrl = $"{Request.Scheme}://{Request.Host}/uploads/{fileName}";
            var filter = Builders<User>.Filter.Eq(x => x.Id, request.UserId);
            var options = new FindOneAndUpdateOptions<User, User>
            {
                ReturnDocument = ReturnDocument.After
            };
            var update = Builders<User>.Update
                .Set(x => x.Avatar, avatarUrl)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);

            var user = await _dbContext.Users.FindOneAndUpdateAsync(filter, update, options);
            if (user is null)
            {
                return NotFound(new { message = "Không tìm thấy tài khoản!" });
            }

            return Ok(new { message = "Cập nhật avatar thành công!", avatarUrl });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi server khi upload: " + ex.Message });
        }
    }

    // ===========================
    // TOGGLE FAVORITE
    // ===========================
    [HttpPost("toggle-favorite")]
    public async Task<IActionResult> ToggleFavorite([FromBody] ToggleFavoriteRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.FoodId))
                return BadRequest(new { message = "Thiếu thông tin!" });

            var user = await _dbContext.Users.Find(x => x.Id == request.UserId).FirstOrDefaultAsync();
            if (user is null)
                return NotFound(new { message = "Không tìm thấy tài khoản!" });

            UpdateDefinition<User> update;
            bool isNowFavorite;

            if (user.FavoriteFoodIds.Contains(request.FoodId))
            {
                update = Builders<User>.Update
                    .Pull(x => x.FavoriteFoodIds, request.FoodId)
                    .Set(x => x.UpdatedAt, DateTime.UtcNow);
                isNowFavorite = false;
            }
            else
            {
                update = Builders<User>.Update
                    .AddToSet(x => x.FavoriteFoodIds, request.FoodId)
                    .Set(x => x.UpdatedAt, DateTime.UtcNow);
                isNowFavorite = true;
            }

            await _dbContext.Users.UpdateOneAsync(x => x.Id == request.UserId, update);
            return Ok(new { isFavorite = isNowFavorite, foodId = request.FoodId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi server: " + ex.Message });
        }
    }

    // ===========================
    // GET USER FAVORITES
    // ===========================
    [HttpGet("{userId}/favorites")]
    public async Task<IActionResult> GetFavorites([FromRoute] string userId)
    {
        try
        {
            var user = await _dbContext.Users.Find(x => x.Id == userId).FirstOrDefaultAsync();
            if (user is null)
                return NotFound(new { message = "Không tìm thấy tài khoản!" });

            if (user.FavoriteFoodIds.Count == 0)
                return Ok(new { foods = Array.Empty<object>(), favoriteFoodIds = user.FavoriteFoodIds });

            var filter = Builders<Food>.Filter.In(x => x.Id, user.FavoriteFoodIds);
            var foods = await _dbContext.Foods.Find(filter).ToListAsync();

            var result = foods.Select(f => new
            {
                id = f.Id,
                name = f.Name,
                description = f.Description,
                imageUrl = f.ImageUrl,
                restaurant = f.Restaurant,
                rating = f.Rating,
                taste = f.Taste,
                tags = f.Tags,
                basePrice = new { coopmart = f.BasePrice.Coopmart, bachhoaxanh = f.BasePrice.Bachhoaxanh },
                currentPrice = new { coopmart = f.CurrentPrice.Coopmart, bachhoaxanh = f.CurrentPrice.Bachhoaxanh },
            });

            return Ok(new { foods = result, favoriteFoodIds = user.FavoriteFoodIds });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi server: " + ex.Message });
        }
    }

    // ===========================
    // CLEAR ALL FAVORITES
    // ===========================
    [HttpDelete("{userId}/favorites/clear")]
    public async Task<IActionResult> ClearFavorites([FromRoute] string userId)
    {
        try
        {
            var user = await _dbContext.Users.Find(x => x.Id == userId).FirstOrDefaultAsync();
            if (user is null)
                return NotFound(new { message = "Không tìm thấy tài khoản!" });

            var update = Builders<User>.Update
                .Set(x => x.FavoriteFoodIds, new List<string>())
                .Set(x => x.UpdatedAt, DateTime.UtcNow);

            await _dbContext.Users.UpdateOneAsync(x => x.Id == userId, update);

            return Ok(new { message = "Đã xóa toàn bộ danh sách yêu thích!" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi server: " + ex.Message });
        }
    }

    // ===========================
    // REMOVE BULK FAVORITES
    // ===========================
    [HttpPost("{userId}/favorites/remove-bulk")]
    public async Task<IActionResult> RemoveBulkFavorites([FromRoute] string userId, [FromBody] RemoveBulkFavoritesRequest request)
    {
        try
        {
            if (request.FoodIds == null || request.FoodIds.Count == 0)
                return BadRequest(new { message = "Không có sản phẩm nào được chọn!" });

            var user = await _dbContext.Users.Find(x => x.Id == userId).FirstOrDefaultAsync();
            if (user is null)
                return NotFound(new { message = "Không tìm thấy tài khoản!" });

            var update = Builders<User>.Update
                .PullAll(x => x.FavoriteFoodIds, request.FoodIds)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);

            await _dbContext.Users.UpdateOneAsync(x => x.Id == userId, update);

            return Ok(new { message = $"Đã xóa {request.FoodIds.Count} sản phẩm khỏi yêu thích!" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi server: " + ex.Message });
        }
    }

    // ===========================
    // ADD HISTORY
    // ===========================
    [HttpPost("{userId}/history")]
    public async Task<IActionResult> AddHistory([FromRoute] string userId, [FromBody] AddHistoryRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(request.FoodId))
                return BadRequest(new { message = "Thiếu thông tin!" });

            var user = await _dbContext.Users.Find(x => x.Id == userId).FirstOrDefaultAsync();
            if (user is null)
                return NotFound(new { message = "Không tìm thấy tài khoản!" });

            var historyItem = new UserHistoryItem
            {
                FoodId = request.FoodId,
                CompletedAt = DateTime.UtcNow
            };

            var update = Builders<User>.Update
                .Push(x => x.CookingHistory, historyItem)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);

            await _dbContext.Users.UpdateOneAsync(x => x.Id == userId, update);

            return Ok(new { message = "Đã lưu lịch sử nấu ăn!" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi server: " + ex.Message });
        }
    }

    // ===========================
    // GET USER HISTORY
    // ===========================
    [HttpGet("{userId}/history")]
    public async Task<IActionResult> GetHistory([FromRoute] string userId)
    {
        try
        {
            var user = await _dbContext.Users.Find(x => x.Id == userId).FirstOrDefaultAsync();
            if (user is null)
                return NotFound(new { message = "Không tìm thấy tài khoản!" });

            if (user.CookingHistory == null || user.CookingHistory.Count == 0)
                return Ok(new { history = Array.Empty<object>(), warning = (string?)null });

            // Fetch food details for history items
            var foodIds = user.CookingHistory.Select(h => h.FoodId).Distinct().ToList();
            var foods = await _dbContext.Foods.Find(Builders<Food>.Filter.In(x => x.Id, foodIds)).ToListAsync();

            var result = user.CookingHistory.OrderByDescending(h => h.CompletedAt).Select(h => {
                var food = foods.FirstOrDefault(f => f.Id == h.FoodId);
                return new {
                    id = h.FoodId,
                    name = food?.Name ?? "Món ăn đã bị xóa",
                    imageUrl = food?.ImageUrl ?? "",
                    completedAt = h.CompletedAt
                };
            }).ToList();

            var today = DateTime.UtcNow.Date;
            var dailyCalories = user.CookingHistory
                .Where(h => h.CompletedAt >= today)
                .Select(h => foods.FirstOrDefault(f => f.Id == h.FoodId))
                .Where(f => f != null)
                .Sum(f => f!.Calories);

            string? warning = null;
            if (dailyCalories > 3000)
            {
                warning = $"Cảnh báo sức khỏe: Bạn đã nạp {dailyCalories} kcal trong ngày hôm nay (vượt ngưỡng 3000 kcal). Hãy chú ý cân bằng dinh dưỡng nhé!";
            }

            return Ok(new { history = result, warning });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi server: " + ex.Message });
        }
    }
}

public class AddHistoryRequest
{
    public string FoodId { get; set; } = string.Empty;
}
