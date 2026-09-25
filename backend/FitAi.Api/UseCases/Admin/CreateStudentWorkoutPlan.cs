using FitAi.Api.Data;
using FitAi.Api.Entities;
using FitAi.Api.UseCases.Shared;
using FitAi.Api.UseCases.WorkoutPlans;
using FitAi.Contracts;

namespace FitAi.Api.UseCases.Admin;

/// <summary>Professor (ou admin) monta um plano para o aluno; ele passa a ser o plano ativo.</summary>
public sealed class CreateStudentWorkoutPlan(AppDbContext db, CreateWorkoutPlan createWorkoutPlan)
{
    public sealed record Input(User Actor, string StudentId, SaveWorkoutPlanRequest Plan);

    public async Task<WorkoutPlanResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var student = await StudentAccess.GetManagedUserAsync(db, input.Actor, input.StudentId, ct);
        return await createWorkoutPlan.ExecuteAsync(
            new(student.Id, input.Plan, WorkoutPlanSource.TEACHER, input.Actor.Id), ct);
    }
}
