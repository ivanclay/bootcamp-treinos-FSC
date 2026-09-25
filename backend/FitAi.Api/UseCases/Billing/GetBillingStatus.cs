using FitAi.Api.Billing;
using FitAi.Api.Data;
using FitAi.Api.Entities;
using FitAi.Api.Options;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FitAi.Api.UseCases.Billing;

/// <summary>Situação da assinatura do professor: plano efetivo, assinatura, cobranças, alunos e preços.</summary>
public sealed class GetBillingStatus(
    AppDbContext db,
    PlanService planService,
    IOptions<PlanOptions> planOptions,
    IOptions<PaymentsOptions> paymentsOptions,
    IWebHostEnvironment environment,
    TimeProvider timeProvider)
{
    public sealed record Input(User Teacher);

    public async Task<BillingStatusResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var subscription = await db.Subscriptions.AsNoTracking()
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.TeacherId == input.Teacher.Id, ct);
        var plan = await planService.GetForUserAsync(input.Teacher, ct);
        var students = await db.Users.CountAsync(u => u.TeacherId == input.Teacher.Id, ct);
        var pro = planOptions.Value.Pro;

        return new BillingStatusResponse(
            plan.Plan,
            subscription?.ToResponse(timeProvider.GetUtcNow(), planService.GracePeriod),
            students,
            planService.FreeLimits,
            new PlanPricesResponse(pro.MonthlyPrice, pro.YearlyPrice),
            CanSimulatePayments: paymentsOptions.Value.IsFake && environment.IsDevelopment());
    }
}
