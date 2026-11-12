using System.Text.RegularExpressions;
using FoodWiseAi.Api.Models;
using FoodWiseAi.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace FoodWiseAi.Api.Controllers;

/// <summary>
/// Controller xử lý việc gửi công thức món ăn qua email cho người dùng.
/// Endpoint: POST /api/foods/{foodId}/send-recipe-email
/// </summary>
[ApiController]
[Route("api/foods")]
public class RecipeEmailController : ControllerBase
{
    private readonly IFoodService _foods;
    private readonly IEmailSender _sender;
    private readonly ILogger<RecipeEmailController>? _logger;
    private readonly IRecipeEmailRateLimiter? _rateLimiter;

    /// <summary>
    /// Constructor tối giản phục vụ cho Unit Test (theo đúng hợp đồng kiểm thử).
    /// </summary>
    public RecipeEmailController(IFoodService foods, IEmailSender sender)
        : this(foods, sender, null, null)
    {
    }

    /// <summary>
    /// Constructor đầy đủ được ASP.NET Core DI kích hoạt khi chạy ứng dụng thật.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public RecipeEmailController(
        IFoodService foods,
        IEmailSender sender,
        ILogger<RecipeEmailController>? logger,
        IRecipeEmailRateLimiter? rateLimiter)
    {
        _foods = foods;
        _sender = sender;
        _logger = logger;
        _rateLimiter = rateLimiter;
    }

    /// <summary>
    /// Gửi email công thức món ăn đến địa chỉ email được chỉ định.
    /// </summary>
    /// <param name="foodId">Mã định danh món ăn (MongoDB ObjectId)</param>
    /// <param name="request">Dữ liệu yêu cầu chứa địa chỉ email nhận</param>
    [HttpPost("{foodId}/send-recipe-email")]
    public async Task<IActionResult> SendRecipe(string foodId, [FromBody] SendRecipeEmailRequest request)
    {
        // 1. Kiểm tra đầu vào: email không được rỗng
        if (request == null || string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "Vui lòng nhập địa chỉ email nhận công thức." });
        }

        var email = request.Email.Trim();

        // 2. Kiểm tra định dạng email
        if (!IsValidEmail(email))
        {
            return BadRequest(new { message = "Địa chỉ email không đúng định dạng. Vui lòng kiểm tra lại." });
        }

        // 3. Giới hạn tần suất gửi (Rate Limiting: tối đa 5 email / 10 phút)
        if (_rateLimiter != null)
        {
            var clientKey = GetClientKey();
            if (!_rateLimiter.IsAllowed(clientKey))
            {
                _logger?.LogWarning("Vượt giới hạn gửi email cho client {ClientKey}", clientKey);
                return StatusCode(429, new
                {
                    message = "Bạn đã gửi quá nhiều yêu cầu trong thời gian ngắn (tối đa 5 email / 10 phút). Vui lòng thử lại sau."
                });
            }
        }

        // 4. Tra cứu món ăn từ CSDL
        Food? food;
        try
        {
            food = await _foods.GetByIdAsync(foodId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Lỗi khi tra cứu món ăn Id={FoodId}", foodId);
            return NotFound(new { message = "Không tìm thấy món ăn với mã được yêu cầu." });
        }

        if (food == null)
        {
            return NotFound(new { message = "Không tìm thấy món ăn với mã được yêu cầu." });
        }

        // 5. Tạo nội dung email (Tiêu đề + HTML an toàn chống XSS + Văn bản thuần)
        var (subject, htmlBody, textBody) = RecipeEmailBuilder.Build(food);

        // 6. Gửi email qua IEmailSender (MailKit SMTP)
        try
        {
            await _sender.SendAsync(email, subject, htmlBody, textBody);

            // Ghi log bảo mật: che bớt ký tự email, không log mật khẩu
            _logger?.LogInformation(
                "[{Time}] Đã gửi công thức món '{FoodName}' (Id: {FoodId}) tới email {MaskedEmail} thành công.",
                DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                food.Name,
                foodId,
                MaskEmail(email));

            return Ok(new
            {
                message = $"Công thức món '{food.Name}' đã được gửi thành công đến {MaskEmail(email)}.",
                foodName = food.Name,
                sentTo = MaskEmail(email)
            });
        }
        catch (Exception ex)
        {
            // Bắt lỗi SMTP, trả về 503 thân thiện, không làm sập ứng dụng
            _logger?.LogError(
                ex,
                "[{Time}] Gửi email công thức món Id={FoodId} tới {MaskedEmail} thất bại.",
                DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                foodId,
                MaskEmail(email));

            return StatusCode(503, new
            {
                message = "Dịch vụ gửi email hiện đang bận hoặc gặp sự cố kết nối máy chủ mail. Vui lòng thử lại sau ít phút."
            });
        }
    }

    /// <summary>
    /// Kiểm tra tính hợp lệ của địa chỉ email (chứa @ và domain có ít nhất 1 dấu chấm).
    /// </summary>
    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;

        // Định dạng tiêu chuẩn: local-part @ domain.tld (không có khoảng trắng, có domain kết thúc bằng tld)
        var regex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
        if (!regex.IsMatch(email)) return false;

        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Lấy định danh IP/Client để áp dụng rate limiting.
    /// </summary>
    private string GetClientKey()
    {
        var forwarded = HttpContext?.Request?.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }
        return HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    }

    /// <summary>
    /// Che giấu bớt ký tự email để ghi log an toàn (ví dụ: "la***@example.com").
    /// </summary>
    public static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return string.Empty;
        var parts = email.Split('@');
        if (parts.Length != 2) return "***";

        var user = parts[0];
        var domain = parts[1];
        var visible = user.Length <= 2 ? user : user[..2];
        return $"{visible}***@{domain}";
    }
}
