using FitAi.Api.Entities;
using FitAi.Contracts;

namespace FitAi.Api.Billing;

public sealed record PlanEvaluation(bool IsPaid, bool InGracePeriod, bool CancelsAtPeriodEnd);

/// <summary>
/// Plano efetivo calculado a cada consulta (sem job agendado): status + validade + carência.
/// Pendente = gratuito até pagar; cancelado vale até o fim do período já pago.
/// </summary>
public static class PlanRules
{
    public static PlanEvaluation Evaluate(Subscription? s, DateTimeOffset now, TimeSpan grace)
    {
        if (s is null || s.Plan != PlanType.PRO) return new(false, false, false);
        return s.Status switch
        {
            SubscriptionStatus.ACTIVE when s.CurrentPeriodEnd is null || now <= s.CurrentPeriodEnd => new(true, false, false),
            SubscriptionStatus.ACTIVE when now <= s.CurrentPeriodEnd + grace => new(true, true, false),
            SubscriptionStatus.PAST_DUE when s.CurrentPeriodEnd is not null && now <= s.CurrentPeriodEnd + grace => new(true, true, false),
            SubscriptionStatus.CANCELED when s.CurrentPeriodEnd is not null && now < s.CurrentPeriodEnd => new(true, false, true),
            _ => new(false, false, false),
        };
    }

    public static DateTimeOffset ExtendPeriod(DateTimeOffset? currentEnd, DateTimeOffset dueDate, DateTimeOffset now, BillingCycle cycle)
    {
        var start = new[] { currentEnd ?? now, dueDate }.Max();
        return cycle == BillingCycle.YEARLY ? start.AddYears(1) : start.AddMonths(1);
    }
}
