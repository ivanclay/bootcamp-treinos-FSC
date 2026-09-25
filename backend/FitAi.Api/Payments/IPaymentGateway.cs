using FitAi.Contracts;

namespace FitAi.Api.Payments;

public sealed record GatewayCustomer(string Name, string Email, string CpfCnpj, string ExternalReference);

public sealed record GatewaySubscription(string Id, string Status);

public sealed record GatewayPayment(
    string Id,
    decimal Value,
    DateTimeOffset DueDate,
    string Status,
    string BillingType,
    string? InvoiceUrl,
    string? BankSlipUrl,
    DateTimeOffset? PaymentDate);

public sealed record GatewayPixQrCode(string EncodedImage, string Payload, DateTimeOffset? ExpirationDate);

/// <summary>Erro do provedor com mensagem segura para log (sem chave nem documento).</summary>
public sealed class PaymentGatewayException(string message, int? statusCode = null, Exception? inner = null)
    : Exception(message, inner)
{
    public int? StatusCode { get; } = statusCode;
}

/// <summary>
/// Provedor de pagamento. Implementações: <see cref="AsaasPaymentGateway"/> (real) e
/// <see cref="FakePaymentGateway"/> (em memória). Cartão e boleto são pagos na fatura hospedada do provedor:
/// este servidor nunca recebe dados de cartão.
/// </summary>
public interface IPaymentGateway
{
    Task<string> EnsureCustomerAsync(GatewayCustomer customer, string? existingCustomerId, CancellationToken ct = default);

    Task<GatewaySubscription> CreateSubscriptionAsync(
        string customerId,
        decimal value,
        BillingCycle cycle,
        PaymentMethod method,
        DateOnly nextDueDate,
        string description,
        string externalReference,
        CancellationToken ct = default);

    Task<IReadOnlyList<GatewayPayment>> ListSubscriptionPaymentsAsync(string subscriptionId, CancellationToken ct = default);

    Task<GatewayPixQrCode> GetPixQrCodeAsync(string paymentId, CancellationToken ct = default);

    Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct = default);
}
