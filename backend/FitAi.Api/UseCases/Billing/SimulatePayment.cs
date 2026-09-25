using FitAi.Api.Data;
using FitAi.Api.Entities;
using FitAi.Api.Errors;
using FitAi.Api.Options;
using FitAi.Api.Payments;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FitAi.Api.UseCases.Billing;

/// <summary>
/// Desenvolvimento com o provedor Fake: gera o mesmo evento PAYMENT_RECEIVED que o Asaas mandaria e o aplica pelo
/// mesmo caminho do webhook (<see cref="HandlePaymentWebhook"/>). Indisponível fora de Development ou com o Asaas.
/// </summary>
public sealed class SimulatePayment(
    AppDbContext db,
    HandlePaymentWebhook handlePaymentWebhook,
    IOptions<PaymentsOptions> paymentsOptions,
    IWebHostEnvironment environment,
    TimeProvider timeProvider)
{
    public sealed record Input(User Teacher);

    public async Task ExecuteAsync(Input input, CancellationToken ct = default)
    {
        if (!paymentsOptions.Value.IsFake || !environment.IsDevelopment()) throw new NotFoundException("Not found");

        var subscription = await db.Subscriptions.AsNoTracking().Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.TeacherId == input.Teacher.Id, ct);
        var payment = subscription?.Payments
            .Where(p => p.Status is PaymentStatus.PENDING or PaymentStatus.OVERDUE)
            .OrderBy(p => p.DueDate)
            .FirstOrDefault()
            ?? throw new NotFoundException("Nenhuma cobrança em aberto");

        var now = timeProvider.GetUtcNow();
        await handlePaymentWebhook.ExecuteAsync(new HandlePaymentWebhook.Input(new PaymentWebhookEvent(
            "evt_sim_" + Guid.NewGuid().ToString("N")[..12], "PAYMENT_RECEIVED", subscription!.ProviderSubscriptionId, payment.ProviderPaymentId,
            payment.Value, payment.DueDate, now, PaymentMapping.ToBillingType(payment.Method), "RECEIVED",
            payment.InvoiceUrl, payment.BankSlipUrl), "fake"), ct);
    }
}
