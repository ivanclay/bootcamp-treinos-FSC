using FitAi.Api.Billing;
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
/// Inicia a assinatura Pro do professor. O preço vem da configuração (nunca do cliente), o documento é validado
/// e uma assinatura pendente já criada é reaproveitada (duplo clique / voltar e reenviar).
/// </summary>
public sealed class StartCheckout(
    AppDbContext db,
    IPaymentGateway gateway,
    PlanService planService,
    PendingPaymentLoader pendingPayment,
    IOptions<PlanOptions> planOptions,
    TimeProvider timeProvider,
    ILogger<StartCheckout> logger)
{
    public sealed record Input(User Teacher, CheckoutRequest Request);

    public async Task<CheckoutResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var request = input.Request;
        var teacher = input.Teacher;
        if (teacher.Role != UserRole.TEACHER) throw new ForbiddenException("Somente professores assinam o Pro");
        if (!Documents.IsValidCpfCnpj(request.CpfCnpj)) throw new ValidationException("CPF ou CNPJ inválido");
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ValidationException("Informe nome e e-mail para a cobrança");
        }

        var plan = await planService.GetForUserAsync(teacher, ct);
        if (plan.Plan == PlanType.PRO) throw new ConflictException("Você já tem o plano Pro ativo");

        var subscription = await db.Subscriptions.Include(s => s.Payments).FirstOrDefaultAsync(s => s.TeacherId == teacher.Id, ct);

        // Reaproveita a assinatura pendente com a mesma forma de pagamento.
        if (subscription is { Status: SubscriptionStatus.PENDING, ProviderSubscriptionId: { } pendingId }
            && subscription.Method == request.Method && subscription.Cycle == request.Cycle)
        {
            return await pendingPayment.LoadAsync(subscription, ct);
        }

        // Pendente com outra forma/ciclo: cancela a antiga no provedor antes de criar a nova.
        if (subscription is { Status: SubscriptionStatus.PENDING, ProviderSubscriptionId: { } oldId })
        {
            await pendingPayment.CallAsync(() => gateway.CancelSubscriptionAsync(oldId, ct));
        }

        var price = request.Cycle == BillingCycle.YEARLY ? planOptions.Value.Pro.YearlyPrice : planOptions.Value.Pro.MonthlyPrice;
        var customerId = await pendingPayment.CallAsync(() => gateway.EnsureCustomerAsync(
            new GatewayCustomer(request.Name.Trim(), request.Email.Trim(), request.CpfCnpj, teacher.Id),
            subscription?.ProviderCustomerId, ct));
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var created = await pendingPayment.CallAsync(() => gateway.CreateSubscriptionAsync(
            customerId, price, request.Cycle, request.Method, today,
            request.Cycle == BillingCycle.YEARLY ? "FIT.AI Pro (anual)" : "FIT.AI Pro (mensal)", teacher.Id, ct));

        try
        {
            if (subscription is null)
            {
                subscription = new Subscription { TeacherId = teacher.Id };
                db.Subscriptions.Add(subscription);
            }
            // Uma linha por professor: uma nova assinatura reaproveita a linha (e o histórico de cobranças).
            subscription.Plan = PlanType.PRO;
            subscription.Cycle = request.Cycle;
            subscription.Method = request.Method;
            subscription.Price = price;
            subscription.Status = SubscriptionStatus.PENDING;
            subscription.CanceledAt = null;
            subscription.ProviderCustomerId = customerId;
            subscription.ProviderSubscriptionId = created.Id;
            // Cobranças em aberto da assinatura anterior deixam de valer (a antiga foi cancelada ou expirou).
            foreach (var old in subscription.Payments.Where(p => p.Status is PaymentStatus.PENDING or PaymentStatus.OVERDUE))
            {
                old.Status = PaymentStatus.CANCELED;
            }
            await db.SaveChangesAsync(ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // Criou no provedor, mas não salvou aqui: registra os ids para conciliação.
            logger.LogError(e, "Checkout saved at provider but not locally: teacher {TeacherId} customer {CustomerId} subscription {SubscriptionId}",
                teacher.Id, customerId, created.Id);
            throw new PaymentProviderException("Não foi possível concluir a assinatura. Tente de novo em instantes.");
        }

        return await pendingPayment.LoadAsync(subscription, ct);
    }
}
