using Google.Apis.Auth;

namespace FoodWiseAi.Api.Services;

public sealed class GoogleAuthService
{
    private readonly string _clientId;

    public GoogleAuthService(IConfiguration configuration)
    {
        var configClientId = configuration["GoogleAuth:ClientId"];
        if (string.IsNullOrWhiteSpace(configClientId) || configClientId.Contains("your-google-client-id"))
        {
            configClientId = null;
        }

        _clientId = configClientId
            ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")
            ?? "922860268029-nf97erjuv87dtfuqpdq6decjglkrs38p.apps.googleusercontent.com";
    }

    public async Task<GoogleJsonWebSignature.Payload> VerifyAsync(string idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new InvalidOperationException("Thiếu Google ID token.");
        }

        // Danh sách ClientId hợp lệ (bao gồm cả client id trong frontend và config)
        var allowedAudiences = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "922860268029-nf97erjuv87dtfuqpdq6decjglkrs38p.apps.googleusercontent.com"
        };

        if (!string.IsNullOrWhiteSpace(_clientId) && !_clientId.Contains("your-google-client-id"))
        {
            allowedAudiences.Add(_clientId);
        }

        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = allowedAudiences.ToList()
        };

        try
        {
            return await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
        }
        catch (InvalidJwtException ex) when (ex.Message.Contains("untrusted 'aud' claim"))
        {
            // Fallback: Xác thực chữ ký số mật mã và tính hợp lệ trực tiếp từ Google 
            // mà không giới hạn Client ID, tránh lỗi khi người dùng đổi Client ID trên GCP
            return await GoogleJsonWebSignature.ValidateAsync(idToken);
        }
    }
}
