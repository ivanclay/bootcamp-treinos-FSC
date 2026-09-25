namespace FitAi.App.Services;

public static class AppConfig
{
    /// <summary>
    /// Endereço da FitAi.Api. No emulador Android, 10.0.2.2 aponta para o localhost da máquina.
    /// Em aparelho físico, troque pelo IP da máquina na rede (ex.: http://192.168.0.10:8080) ou pela URL publicada.
    /// </summary>
#if DEBUG
    public const string ApiBaseUrl = "http://10.0.2.2:8080";
#else
    // Release: HTTP sem TLS é bloqueado (MainApplication.cs); use a URL HTTPS da API publicada.
    public const string ApiBaseUrl = "https://api.seu-dominio.com.br";
#endif

    /// <summary>Esquema do deep link de retorno do login (registrado em Platforms/Android/WebAuthenticatorCallbackActivity).</summary>
    public const string CallbackScheme = "fitai";

    public const string CallbackUri = CallbackScheme + "://auth";
}
