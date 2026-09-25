using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FitAi.Api.Payments;

/// <summary>Evento de pagamento já normalizado (independente do provedor).</summary>
public sealed record PaymentWebhookEvent(
    string EventId,
    string Event,
    string? SubscriptionId,
    string? PaymentId,
    decimal? Value,
    DateTimeOffset? DueDate,
    DateTimeOffset? PaymentDate,
    string? BillingType,
    string? Status,
    string? InvoiceUrl,
    string? BankSlipUrl);

public static class AsaasWebhook
{
    /// <summary>Compara em tempo constante. Token não configurado (vazio) rejeita tudo.</summary>
    public static bool IsTokenValid(string? configured, string? provided)
    {
        if (string.IsNullOrEmpty(configured) || string.IsNullOrEmpty(provided)) return false;
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(configured)),
            SHA256.HashData(Encoding.UTF8.GetBytes(provided)));
    }

    /// <summary>Converte o JSON do Asaas (evento de cobrança ou de assinatura). Retorna nulo se inválido.</summary>
    public static PaymentWebhookEvent? Map(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            var id = Str(root, "id");
            var evt = Str(root, "event");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(evt) || id.Length > 100) return null;

            if (root.TryGetProperty("payment", out var p) && p.ValueKind == JsonValueKind.Object)
            {
                return new PaymentWebhookEvent(
                    id, evt, Str(p, "subscription"), Str(p, "id"),
                    p.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null,
                    PaymentMapping.ParseDate(Str(p, "dueDate")), PaymentMapping.ParseDate(Str(p, "paymentDate")),
                    Str(p, "billingType"), Str(p, "status"), Str(p, "invoiceUrl"), Str(p, "bankSlipUrl"));
            }

            if (root.TryGetProperty("subscription", out var s) && s.ValueKind == JsonValueKind.Object)
            {
                return new PaymentWebhookEvent(id, evt, Str(s, "id"), null, null, null, null, null, Str(s, "status"), null, null);
            }

            return new PaymentWebhookEvent(id, evt, null, null, null, null, null, null, null, null, null);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
