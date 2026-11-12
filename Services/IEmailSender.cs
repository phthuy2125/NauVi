namespace FoodWiseAi.Api.Services;

/// <summary>
/// Giao diện dịch vụ gửi email để dễ dàng thay thế và viết unit test mock.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, string textBody);
}
