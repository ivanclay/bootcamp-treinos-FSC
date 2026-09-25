using FitAi.Api.Data;
using FitAi.Api.Entities;
using FitAi.Api.Errors;
using FitAi.Api.Payments;
using FitAi.Api.UseCases.Billing;
using FitAi.Contracts;

namespace FitAi.Api.Billing;

/// <summary>
/// Sincroniza as cobranças de uma assinatura com o provedor e monta a resposta da cobrança em aberto
/// (QR PIX ou fatura). Traduz erros do provedor em mensagem genérica, sem detalhes internos.
/// </summary>
public sealed class PendingPaymentLoader(
    AppDbContext db,
    IPaymentGateway gateway,
    PlanService planService,
    TimeProvider timeProvider,
    ILogger<PendingPaymentLoader> logger)
{
    public async Task<CheckoutResponse> LoadAsync(Subscription subscription, CancellationToken ct)
    {
        var providerSubscriptionId = subscription.ProviderSubscriptionId
            ?? throw new NotFoundException("Nenhuma assinatura aguardando pagamento");
        var payments = await CallAsync(() => gateway.ListSubscriptionPaymentsAsync(providerSubscriptionId, ct));
        foreach (var payment in payments) subscription.Upsert(payment);
        await db.SaveChangesAsync(ct);

        var current = payments.Select(p => p.Id).ToHashSet();
        var open = subscription.Payments
            .Where(p => current.Contains(p.ProviderPaymentId) && p.Status is PaymentStatus.PENDING or PaymentStatus.OVERDUE)
            .OrderBy(p => p.DueDate)
            .FirstOrDefault()
            ?? throw new PaymentProviderException("O provedor ainda não gerou a cobrança. Tente de novo em instantes.");

        PixQrCodeResponse? pix = null;
        if (open.Method == PaymentMethod.PIX)
        {
            var qr = await CallAsync(() => gateway.GetPixQrCodeAsync(open.ProviderPaymentId, ct));
            pix = new PixQrCodeResponse(qr.EncodedImage, qr.Payload, qr.ExpirationDate);
        }

        return new CheckoutResponse(subscription.ToResponse(timeProvider.GetUtcNow(), planService.GracePeriod), open.ToResponse(), pix);
    }

    public async Task<T> CallAsync<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch (PaymentGatewayException e)
        {
            logger.LogWarning("Payment provider error: {Message} ({Status})", e.Message, e.StatusCode);
            throw new PaymentProviderException("O serviço de pagamento não respondeu como esperado. Tente de novo em instantes.");
        }
    }

    public Task CallAsync(Func<Task> action) => CallAsync(async () => { await action(); return true; });
}
