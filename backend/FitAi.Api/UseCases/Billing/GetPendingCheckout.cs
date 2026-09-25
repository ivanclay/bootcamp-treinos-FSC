using FitAi.Api.Billing;
using FitAi.Api.Data;
using FitAi.Api.Entities;
using FitAi.Api.Errors;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.Billing;

/// <summary>Cobrança em aberto da assinatura pendente (tela "Aguardando pagamento").</summary>
public sealed class GetPendingCheckout(AppDbContext db, PendingPaymentLoader pendingPayment)
{
    public sealed record Input(User Teacher);

    public async Task<CheckoutResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var subscription = await db.Subscriptions.Include(s => s.Payments).FirstOrDefaultAsync(s => s.TeacherId == input.Teacher.Id, ct);
        if (subscription is not { Status: SubscriptionStatus.PENDING, ProviderSubscriptionId: not null })
        {
            throw new NotFoundException("Nenhuma assinatura aguardando pagamento");
        }
        return await pendingPayment.LoadAsync(subscription, ct);
    }
}
