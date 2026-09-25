using FitAi.Api.Billing;
using FitAi.Api.Data;
using FitAi.Api.Entities;
using FitAi.Api.Errors;
using FitAi.Api.Payments;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.Billing;

/// <summary>Cancela no provedor; o Pro continua valendo até o fim do período já pago.</summary>
public sealed class CancelSubscription(
    AppDbContext db, IPaymentGateway gateway, PendingPaymentLoader pendingPayment, PlanService planService, TimeProvider timeProvider)
{
    public sealed record Input(User Teacher);

    public async Task<SubscriptionResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var subscription = await db.Subscriptions.Include(s => s.Payments).FirstOrDefaultAsync(s => s.TeacherId == input.Teacher.Id, ct);
        if (subscription is null || subscription.Status == SubscriptionStatus.CANCELED)
        {
            throw new NotFoundException("Nenhuma assinatura ativa");
        }
        if (subscription.ProviderSubscriptionId is { } providerId)
        {
            await pendingPayment.CallAsync(() => gateway.CancelSubscriptionAsync(providerId, ct));
        }
        subscription.Status = SubscriptionStatus.CANCELED;
        subscription.CanceledAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return subscription.ToResponse(timeProvider.GetUtcNow(), planService.GracePeriod);
    }
}
