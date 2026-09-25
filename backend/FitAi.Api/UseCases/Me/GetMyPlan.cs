using FitAi.Api.Billing;
using FitAi.Api.Entities;
using FitAi.Contracts;

namespace FitAi.Api.UseCases.Me;

/// <summary>Plano efetivo de quem está logado e o uso do mês (Coach AI e planos gerados pela IA).</summary>
public sealed class GetMyPlan(PlanService planService)
{
    public sealed record Input(User User);

    public async Task<MyPlanResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var plan = await planService.GetForUserAsync(input.User, ct);
        return new MyPlanResponse(
            plan.Plan,
            plan.SponsorName,
            plan.Limits,
            await planService.GetCoachMessagesUsedAsync(input.User.Id, ct),
            await planService.CountAiPlansThisMonthAsync(input.User.Id, ct),
            plan.InGracePeriod);
    }
}
