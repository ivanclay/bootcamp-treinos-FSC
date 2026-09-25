using FitAi.Api.Billing;
using FitAi.Api.Data;
using FitAi.Api.Entities;
using FitAi.Api.Payments;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.Billing;

/// <summary>
/// Aplica um evento do provedor. O webhook é a fonte da verdade do pagamento.
/// Idempotência atômica: reivindica o id do evento ANTES de qualquer efeito (INSERT ... ON CONFLICT DO NOTHING)
/// e libera a reivindicação se algo falhar, para o reenvio do provedor ser processado.
/// </summary>
public sealed class HandlePaymentWebhook(AppDbContext db, TimeProvider timeProvider, ILogger<HandlePaymentWebhook> logger)
{
    public sealed record Input(PaymentWebhookEvent Event, string Provider = "asaas");

    /// <returns>true se aplicou; false se duplicado ou desconhecido.</returns>
    public async Task<bool> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var evt = input.Event;
        var now = timeProvider.GetUtcNow();
        var claimed = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "WebhookEvents" ("Id", "Provider", "EventType", "ReceivedAt")
            VALUES ({evt.EventId}, {input.Provider}, {evt.Event}, {now})
            ON CONFLICT ("Id") DO NOTHING
            """, ct);
        if (claimed == 0)
        {
            logger.LogInformation("Webhook {EventId} ignored (duplicate)", evt.EventId);
            return false;
        }

        try
        {
            var subscription = await FindSubscriptionAsync(evt, ct);
            if (subscription is null)
            {
                logger.LogWarning("Webhook {EventId} ({Event}) ignored: subscription not found", evt.EventId, evt.Event);
                return false;
            }

            var wasPaid = UpsertPayment(subscription, evt, out var payment);
            Apply(subscription, evt, payment, wasPaid, now);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Webhook {EventId} ({Event}) applied to subscription {SubscriptionId}", evt.EventId, evt.Event, subscription.Id);
            return true;
        }
        catch
        {
            await db.WebhookEvents.Where(w => w.Id == evt.EventId).ExecuteDeleteAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<Subscription?> FindSubscriptionAsync(PaymentWebhookEvent evt, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(evt.SubscriptionId))
        {
            var bySubscription = await db.Subscriptions.Include(s => s.Payments)
                .FirstOrDefaultAsync(s => s.ProviderSubscriptionId == evt.SubscriptionId, ct);
            if (bySubscription is not null) return bySubscription;
        }
        if (!string.IsNullOrEmpty(evt.PaymentId))
        {
            var payment = await db.PaymentRecords.Include(p => p.Subscription).ThenInclude(s => s.Payments)
                .FirstOrDefaultAsync(p => p.ProviderPaymentId == evt.PaymentId, ct);
            return payment?.Subscription;
        }
        return null;
    }

    /// <returns>Se a cobrança JÁ estava paga antes deste evento.</returns>
    private static bool UpsertPayment(Subscription subscription, PaymentWebhookEvent evt, out PaymentRecord? record)
    {
        record = null;
        if (string.IsNullOrEmpty(evt.PaymentId)) return false;

        var existing = subscription.Payments.FirstOrDefault(p => p.ProviderPaymentId == evt.PaymentId);
        var wasPaid = existing?.IsPaid ?? false;
        record = subscription.Upsert(new GatewayPayment(
            evt.PaymentId,
            evt.Value ?? existing?.Value ?? subscription.Price,
            evt.DueDate ?? existing?.DueDate ?? DateTimeOffset.UtcNow,
            evt.Status ?? StatusFromEvent(evt.Event) ?? existing?.Status.ToString() ?? "PENDING",
            evt.BillingType ?? (existing is null ? PaymentMapping.ToBillingType(subscription.Method) : PaymentMapping.ToBillingType(existing.Method)),
            evt.InvoiceUrl ?? existing?.InvoiceUrl,
            evt.BankSlipUrl ?? existing?.BankSlipUrl,
            evt.PaymentDate ?? existing?.PaidAt));

        // O evento manda no status (ex.: PAYMENT_RECEIVED com status desatualizado no corpo).
        if (StatusFromEvent(evt.Event) is { } fromEvent) record.Status = PaymentMapping.FromStatus(fromEvent);
        if (record.IsPaid && record.PaidAt is null) record.PaidAt = DateTimeOffset.UtcNow;
        return wasPaid;
    }

    private static string? StatusFromEvent(string evt) => evt switch
    {
        "PAYMENT_CONFIRMED" => "CONFIRMED",
        "PAYMENT_RECEIVED" => "RECEIVED",
        "PAYMENT_OVERDUE" => "OVERDUE",
        "PAYMENT_REFUNDED" => "REFUNDED",
        "PAYMENT_DELETED" => "DELETED",
        _ => null,
    };

    private static void Apply(Subscription subscription, PaymentWebhookEvent evt, PaymentRecord? payment, bool wasPaid, DateTimeOffset now)
    {
        switch (evt.Event)
        {
            case "PAYMENT_CONFIRMED" or "PAYMENT_RECEIVED" when payment is not null:
                subscription.Status = SubscriptionStatus.ACTIVE;
                subscription.CanceledAt = null;
                // Cartão manda CONFIRMED e depois RECEIVED: só estende na 1ª vez que a cobrança fica paga.
                if (!wasPaid)
                {
                    subscription.CurrentPeriodEnd = PlanRules.ExtendPeriod(subscription.CurrentPeriodEnd, payment.DueDate, now, subscription.Cycle);
                }
                break;
            case "PAYMENT_OVERDUE" when subscription.Status == SubscriptionStatus.ACTIVE:
                subscription.Status = SubscriptionStatus.PAST_DUE;
                break;
            case "SUBSCRIPTION_DELETED" or "SUBSCRIPTION_INACTIVATED":
                if (subscription.Status != SubscriptionStatus.CANCELED)
                {
                    subscription.Status = SubscriptionStatus.CANCELED;
                    subscription.CanceledAt = now;
                }
                break;
        }
    }
}
