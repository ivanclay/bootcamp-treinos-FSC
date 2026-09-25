namespace FitAi.Api.Options;

/// <summary>Limites e preços (ajuste comercial sem deploy).</summary>
public sealed class PlanOptions
{
    public const string Section = "Plans";

    public FreePlanOptions Free { get; set; } = new();
    public ProPlanOptions Pro { get; set; } = new();
}

public sealed class FreePlanOptions
{
    public int MaxStudents { get; set; } = 5;
    public int CoachMessagesPerMonth { get; set; } = 30;
    public int AiPlansPerMonth { get; set; } = 1;
    public int HistoryDays { get; set; } = 30;
}

public sealed class ProPlanOptions
{
    public decimal MonthlyPrice { get; set; } = 29.90m;
    public decimal YearlyPrice { get; set; } = 299.00m;

    /// <summary>Dias de tolerância depois do vencimento antes de voltar ao gratuito.</summary>
    public int GracePeriodDays { get; set; } = 7;
}

public sealed class PaymentsOptions
{
    public const string Section = "Payments";

    /// <summary>"Fake" (em memória, desenvolvimento) ou "Asaas".</summary>
    public string Provider { get; set; } = "Fake";

    public bool IsFake => !string.Equals(Provider, "Asaas", StringComparison.OrdinalIgnoreCase);
}

public sealed class AsaasOptions
{
    public const string Section = "Asaas";

    public string BaseUrl { get; set; } = "https://api-sandbox.asaas.com/v3/";

    /// <summary>Segredo: só por variável de ambiente/cofre (Asaas__ApiKey).</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Token cadastrado no webhook do painel do Asaas (Asaas__WebhookToken). Vazio rejeita todo webhook.</summary>
    public string WebhookToken { get; set; } = "";
}
