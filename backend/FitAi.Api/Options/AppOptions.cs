namespace FitAi.Api.Options;

public sealed class AuthOptions
{
    public const string Section = "Auth";

    public string JwtSecret { get; set; } = "";
    public string JwtIssuer { get; set; } = "fitai-api";
    public string JwtAudience { get; set; } = "fitai-clients";
    public int TokenLifetimeDays { get; set; } = 7;

    /// <summary>E-mails que viram ADMIN a cada login.</summary>
    public List<string> AdminEmails { get; set; } = [];

    /// <summary>Destinos aceitos após o login (prefixos). Ex.: https://localhost:7001/auth/callback, fitai://auth</summary>
    public List<string> AllowedRedirectUris { get; set; } = [];

    /// <summary>Permite POST /auth/dev-login (somente em Development).</summary>
    public bool EnableDevLogin { get; set; }

    public GoogleAuthOptions Google { get; set; } = new();
}

public sealed class GoogleAuthOptions
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}

public sealed class AiOptions
{
    public const string Section = "Ai";

    /// <summary>Nome do provedor usado quando nada foi escolhido no painel.</summary>
    public string DefaultProvider { get; set; } = "OpenAI";

    public Dictionary<string, AiProviderOptions> Providers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Limite de chamadas de tools por mensagem.</summary>
    public int MaxToolIterations { get; set; } = 10;
}

public sealed class AiProviderOptions
{
    /// <summary>"OpenAI" (qualquer endpoint compatível com a API da OpenAI) ou "AzureOpenAI".</summary>
    public string Type { get; set; } = "OpenAI";

    /// <summary>Opcional para a OpenAI; obrigatório para os demais.</summary>
    public string? Endpoint { get; set; }

    public string? ApiKey { get; set; }

    /// <summary>Modelo (ou deployment, no Azure) padrão.</summary>
    public string Model { get; set; } = "";

    /// <summary>Falso para provedores locais sem autenticação (ex.: Ollama).</summary>
    public bool RequiresApiKey { get; set; } = true;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Model) &&
        (!RequiresApiKey || !string.IsNullOrWhiteSpace(ApiKey)) &&
        (Type.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(Endpoint));
}

public sealed class YouTubeOptions
{
    public const string Section = "YouTube";

    public string? ApiKey { get; set; }
}
