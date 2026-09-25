using System.Collections.Concurrent;
using FitAi.Contracts;

namespace FitAi.Api.Payments;

/// <summary>
/// Provedor em memória para desenvolvimento e testes (Payments:Provider=Fake). Cria a 1ª cobrança pendente
/// junto com a assinatura. Não há atalho para "marcar pago": o pagamento só confirma pelo webhook,
/// o mesmo caminho do provedor real.
/// </summary>
public sealed class FakePaymentGateway(TimeProvider timeProvider) : IPaymentGateway
{
    // QR ilustrativo (não é um PIX válido).
    private const string FakeQrPng = "iVBORw0KGgoAAAANSUhEUgAAAOgAAADoAQAAAADN0pXVAAABAklEQVR42u2YMXLEMAwDd2/y/y8jBSVLbtIHiQuPR6zWJEBShh+eD//R3x0lO8HZ73OSPt6vTSpANJjnpJMXJ69gFv6ctPKuR0BSrd+LdyXX1PPmAXZxp5nX26C3fEt57+rFPE5V3J1dZiVRBtnWfvSBKJBRryfR6a3nDbxA7fUr3w1oKXi7V+t07eh3yZgQtNafty0LYTnW1HTlfpQQEnLmDDKLUuW8scwqsxftrcFe/QY3oquwHceqzG+ugep0J2r96j0+e31Tfb/hyPiMIGntv85ESYhXxttv8zJ9d35AOv3qFRUX8arqUt7kXMYC04t77ye9nNlnZeidn4sV+uej35IvdsqhevEZAAAAAElFTkSuQmCC";

    private readonly ConcurrentDictionary<string, List<GatewayPayment>> _payments = new();
    private readonly ConcurrentDictionary<string, string> _subscriptionStatus = new();

    public Task<string> EnsureCustomerAsync(GatewayCustomer customer, string? existingCustomerId, CancellationToken ct = default) =>
        Task.FromResult(string.IsNullOrWhiteSpace(existingCustomerId) ? "cus_fake_" + Guid.NewGuid().ToString("N")[..12] : existingCustomerId);

    public Task<GatewaySubscription> CreateSubscriptionAsync(
        string customerId, decimal value, BillingCycle cycle, PaymentMethod method, DateOnly nextDueDate,
        string description, string externalReference, CancellationToken ct = default)
    {
        var subscriptionId = "sub_fake_" + Guid.NewGuid().ToString("N")[..12];
        var paymentId = "pay_fake_" + Guid.NewGuid().ToString("N")[..12];
        var due = new DateTimeOffset(nextDueDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var invoice = method == PaymentMethod.PIX ? null : "https://sandbox.asaas.com/i/" + paymentId;
        _payments[subscriptionId] =
        [
            new GatewayPayment(paymentId, value, due, "PENDING", PaymentMapping.ToBillingType(method), invoice,
                method == PaymentMethod.BOLETO ? invoice + "/boleto" : null, null),
        ];
        _subscriptionStatus[subscriptionId] = "ACTIVE";
        return Task.FromResult(new GatewaySubscription(subscriptionId, "ACTIVE"));
    }

    public Task<IReadOnlyList<GatewayPayment>> ListSubscriptionPaymentsAsync(string subscriptionId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<GatewayPayment>>(_payments.TryGetValue(subscriptionId, out var list) ? [.. list] : []);

    public Task<GatewayPixQrCode> GetPixQrCodeAsync(string paymentId, CancellationToken ct = default) =>
        Task.FromResult(new GatewayPixQrCode(
            FakeQrPng,
            "00020126580014br.gov.bcb.pix0136fitai-fake-" + paymentId + "5204000053039865802BR5906FITAI6009SAO PAULO6304ABCD",
            timeProvider.GetUtcNow().AddHours(24)));

    public Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct = default)
    {
        _subscriptionStatus[subscriptionId] = "INACTIVE";
        return Task.CompletedTask;
    }
}
