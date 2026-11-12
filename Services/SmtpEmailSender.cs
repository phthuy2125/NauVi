using FoodWiseAi.Api.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FoodWiseAi.Api.Services;

/// <summary>
/// Triển khai gửi email thực tế qua giao thức SMTP sử dụng thư viện MailKit.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpSettings> options, ILogger<SmtpEmailSender> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, string textBody)
    {
        var message = new MimeMessage();

        var senderAddress = string.IsNullOrWhiteSpace(_settings.SenderEmail) ? _settings.UserName : _settings.SenderEmail;
        message.From.Add(new MailboxAddress(_settings.SenderName, senderAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        var builder = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = textBody
        };
        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        client.Timeout = 15000; // 15 giây timeout tránh treo request

        try
        {
            var secureOption = _settings.Port == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;

            await client.ConnectAsync(_settings.Host, _settings.Port, secureOption);

            if (string.IsNullOrWhiteSpace(_settings.UserName) || string.IsNullOrWhiteSpace(_settings.Password))
            {
                _logger.LogError("Chưa cấu hình tài khoản SMTP (UserName hoặc Password đang trống). Vui lòng điền vào appsettings.json hoặc .env");
                throw new InvalidOperationException("Chưa cấu hình UserName hoặc Password trong SmtpSettings.");
            }

            await client.AuthenticateAsync(_settings.UserName, _settings.Password);
            await client.SendAsync(message);
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true);
            }
        }
    }
}
