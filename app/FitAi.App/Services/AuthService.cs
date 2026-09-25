using System.Text.Json;
using FitAi.Contracts;

namespace FitAi.App.Services;

/// <summary>
/// Login com Google pelo navegador do sistema (WebAuthenticator): a API faz o OAuth e volta para
/// fitai://auth?code=..., e o código é trocado por um JWT, guardado no SecureStorage.
/// PKCE: o verifier nunca sai do app, então outro app que capture o deep link fitai:// não consegue usar o código.
/// </summary>
public sealed class AuthService(ApiClient api)
{
    private const string TokenKey = "fitai.token";
    private const string ExpiresKey = "fitai.expires";
    private const string UserKey = "fitai.user";

    public CurrentUserResponse? User { get; private set; }

    public async Task<bool> RestoreAsync()
    {
        try
        {
            var token = await SecureStorage.Default.GetAsync(TokenKey);
            var expires = await SecureStorage.Default.GetAsync(ExpiresKey);
            if (string.IsNullOrEmpty(token) || !DateTimeOffset.TryParse(expires, out var expiresAt) || expiresAt <= DateTimeOffset.UtcNow)
            {
                return false;
            }
            api.AccessToken = token;
            var userJson = await SecureStorage.Default.GetAsync(UserKey);
            User = userJson is null ? null : JsonSerializer.Deserialize<CurrentUserResponse>(userJson, JsonSerializerOptions.Web);
            return true;
        }
        catch (Exception)
        {
            // SecureStorage pode falhar após reinstalar o app (chave do Keystore perdida).
            SecureStorage.Default.RemoveAll();
            return false;
        }
    }

    public async Task<CurrentUserResponse> LoginWithGoogleAsync()
    {
        var verifier = Pkce.CreateVerifier();
        var result = await WebAuthenticator.Default.AuthenticateAsync(new WebAuthenticatorOptions
        {
            Url = new Uri(ApiClient.GoogleLoginUrl(Pkce.CreateChallenge(verifier))),
            CallbackUrl = new Uri(AppConfig.CallbackUri),
            PrefersEphemeralWebBrowserSession = true,
        });

        if (result.Properties.TryGetValue("error", out var error))
        {
            throw new ApiException(System.Net.HttpStatusCode.Unauthorized, error == "blocked" ? "Sua conta está bloqueada." : "Não foi possível entrar.", error);
        }
        if (!result.Properties.TryGetValue("code", out var code))
        {
            throw new ApiException(System.Net.HttpStatusCode.Unauthorized, "Não foi possível entrar.", "login_failed");
        }

        return await SaveAsync(await api.ExchangeCodeAsync(code, verifier));
    }

    public async Task<CurrentUserResponse> DevLoginAsync(string email) => await SaveAsync(await api.DevLoginAsync(email));

    public async Task<CurrentUserResponse> RefreshUserAsync()
    {
        User = await api.GetCurrentUserAsync();
        await SecureStorage.Default.SetAsync(UserKey, JsonSerializer.Serialize(User, JsonSerializerOptions.Web));
        return User;
    }

    public void Logout()
    {
        api.AccessToken = null;
        User = null;
        SecureStorage.Default.RemoveAll();
    }

    private async Task<CurrentUserResponse> SaveAsync(AuthTokenResponse auth)
    {
        api.AccessToken = auth.AccessToken;
        User = auth.User;
        await SecureStorage.Default.SetAsync(TokenKey, auth.AccessToken);
        await SecureStorage.Default.SetAsync(ExpiresKey, auth.ExpiresAt.ToString("O"));
        await SecureStorage.Default.SetAsync(UserKey, JsonSerializer.Serialize(auth.User, JsonSerializerOptions.Web));
        return auth.User;
    }
}
