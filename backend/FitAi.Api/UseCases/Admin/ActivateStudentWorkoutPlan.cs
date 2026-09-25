using FitAi.Api.Data;
using FitAi.Api.Entities;
using FitAi.Api.UseCases.Shared;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.Admin;

public sealed class ActivateStudentWorkoutPlan(AppDbContext db)
{
    public sealed record Input(User Actor, Guid WorkoutPlanId);

    public async Task<WorkoutPlanResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var plan = await StudentAccess.GetManagedPlanAsync(db, input.Actor, input.WorkoutPlanId, ct);
        var others = await db.WorkoutPlans.Where(p => p.UserId == plan.UserId && p.IsActive && p.Id != plan.Id).ToListAsync(ct);
        foreach (var other in others) other.IsActive = false;
        plan.IsActive = true;
        await db.SaveChangesAsync(ct);
        return plan.ToResponse();
    }
}
