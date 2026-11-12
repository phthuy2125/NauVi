namespace FoodWiseAi.Api.Models;

/// <summary>
/// Cấu hình kết nối máy chủ SMTP để gửi email qua MailKit.
/// Được nạp tự động từ appsettings.json.
/// </summary>
public sealed class SmtpSettings
{
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = false;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = "NauVi - Nền tảng ẩm thực";
}
