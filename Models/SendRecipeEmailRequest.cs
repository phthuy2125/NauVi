namespace FoodWiseAi.Api.Models;

/// <summary>
/// DTO nhận yêu cầu gửi công thức món ăn qua email.
/// </summary>
public sealed class SendRecipeEmailRequest
{
    public string Email { get; set; } = string.Empty;
}
