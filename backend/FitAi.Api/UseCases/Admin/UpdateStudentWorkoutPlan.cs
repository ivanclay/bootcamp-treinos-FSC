using FitAi.Api.Data;
using FitAi.Api.Entities;
using FitAi.Api.UseCases.Shared;
using FitAi.Api.UseCases.WorkoutPlans;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.Admin;

/// <summary>
/// Edita um plano existente. Os dias são casados pelo dia da semana: dias mantidos preservam
/// o histórico de sessões; os exercícios de cada dia são substituídos.
/// </summary>
public sealed class UpdateStudentWorkoutPlan(AppDbContext db)
{
    public sealed record Input(User Actor, Guid WorkoutPlanId, SaveWorkoutPlanRequest Plan);

    public async Task<WorkoutPlanResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        WorkoutPlanValidator.Validate(input.Plan);
        var plan = await StudentAccess.GetManagedPlanAsync(db, input.Actor, input.WorkoutPlanId, ct);

        plan.Name = input.Plan.Name.Trim();
        plan.Goal = input.Plan.Goal;
        plan.Source = WorkoutPlanSource.TEACHER;
        plan.CreatedById = input.Actor.Id;

        var incoming = input.Plan.WorkoutDays.ToEntities().ToDictionary(d => d.WeekDay);
        foreach (var day in plan.WorkoutDays.ToList())
        {
            if (!incoming.Remove(day.WeekDay, out var updated))
            {
                db.WorkoutDays.Remove(day);
                continue;
            }
            day.Name = updated.Name;
            day.IsRest = updated.IsRest;
            day.EstimatedDurationInSeconds = updated.EstimatedDurationInSeconds;
            day.CoverImageUrl = updated.CoverImageUrl;
            db.WorkoutExercises.RemoveRange(day.Exercises);
            foreach (var exercise in updated.Exercises)
            {
                exercise.WorkoutDayId = day.Id;
                db.WorkoutExercises.Add(exercise);
            }
        }
        foreach (var newDay in incoming.Values)
        {
            newDay.WorkoutPlanId = plan.Id;
            db.WorkoutDays.Add(newDay);
        }

        plan.CoverImageUrl = string.IsNullOrWhiteSpace(input.Plan.CoverImageUrl)
            ? input.Plan.WorkoutDays.FirstOrDefault(d => !d.IsRest && !string.IsNullOrWhiteSpace(d.CoverImageUrl))?.CoverImageUrl
            : input.Plan.CoverImageUrl;

        await db.SaveChangesAsync(ct);

        var saved = await db.WorkoutPlans.AsNoTracking()
            .Include(p => p.WorkoutDays).ThenInclude(d => d.Exercises)
            .FirstAsync(p => p.Id == plan.Id, ct);
        return saved.ToResponse();
    }
}
