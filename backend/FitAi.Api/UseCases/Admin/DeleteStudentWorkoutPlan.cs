using FitAi.Api.Data;
using FitAi.Api.Entities;
using FitAi.Api.UseCases.Shared;

namespace FitAi.Api.UseCases.Admin;

/// <summary>Exclui o plano (dias, exercícios e sessões em cascata).</summary>
public sealed class DeleteStudentWorkoutPlan(AppDbContext db)
{
    public sealed record Input(User Actor, Guid WorkoutPlanId);

    public async Task ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var plan = await StudentAccess.GetManagedPlanAsync(db, input.Actor, input.WorkoutPlanId, ct);
        db.WorkoutPlans.Remove(plan);
        await db.SaveChangesAsync(ct);
    }
}
