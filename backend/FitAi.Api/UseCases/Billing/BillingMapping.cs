using FitAi.Api.Billing;
using FitAi.Api.Entities;
using FitAi.Api.Payments;
using FitAi.Contracts;

namespace FitAi.Api.UseCases.Billing;

public static class BillingMapping
{
    public static SubscriptionResponse ToResponse(this Subscription s, DateTimeOffset now, TimeSpan grace)
    {
        var evaluation = PlanRules.Evaluate(s, now, grace);
        return new SubscriptionResponse(
            s.Id, s.Status, s.Cycle, s.Method, s.Price, s.CurrentPeriodEnd, s.CanceledAt,
            evaluation.InGracePeriod, evaluation.CancelsAtPeriodEnd,
            s.Payments.OrderByDescending(p => p.DueDate).Select(p => p.ToResponse()).ToList());
    }

    public static PaymentResponse ToResponse(this PaymentRecord p) =>
        new(p.ProviderPaymentId, p.Value, p.DueDate, p.Status, p.Method, p.PaidAt, p.InvoiceUrl, p.BankSlipUrl);

    /// <summary>Atualiza (ou cria) o espelho local de uma cobrança do provedor.</summary>
    public static PaymentRecord Upsert(this Subscription subscription, GatewayPayment payment)
    {
        var record = subscription.Payments.FirstOrDefault(p => p.ProviderPaymentId == payment.Id);
        if (record is null)
        {
            record = new PaymentRecord { ProviderPaymentId = payment.Id };
            subscription.Payments.Add(record);
        }
        record.Value = payment.Value;
        record.DueDate = payment.DueDate;
        record.Status = PaymentMapping.FromStatus(payment.Status);
        record.Method = PaymentMapping.FromBillingType(payment.BillingType);
        record.PaidAt = payment.PaymentDate;
        record.InvoiceUrl = payment.InvoiceUrl;
        record.BankSlipUrl = payment.BankSlipUrl;
        return record;
    }
}
