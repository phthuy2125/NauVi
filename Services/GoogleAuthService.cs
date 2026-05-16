using Google.Apis.Auth;

namespace FoodWiseAi.Api.Services;

public sealed class GoogleAuthService
{
    private readonly string _clientId;

    public GoogleAuthService(IConfiguration configuration)
    {
        _clientId = configuration["GoogleAuth:ClientId"]
            ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")
            ?? "922860268029-nf97erjuv87dtfuqpdq6decjglkrs38p.apps.googleusercontent.com";
    }

    public async Task<GoogleJsonWebSignature.Payload> VerifyAsync(string idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new InvalidOperationException("Thiếu Google ID token.");
        }

        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = new[] { _clientId }
        };

        return await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
    }
}
