using FitAi.Api.Data;
using FitAi.Api.Errors;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.WorkoutPlans;

public sealed class GetWorkoutPlan(AppDbContext db)
{
    public sealed record Input(string UserId, Guid WorkoutPlanId);

    public async Task<WorkoutPlanSummaryResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var plan = await db.WorkoutPlans.AsNoTracking()
            .Where(p => p.Id == input.WorkoutPlanId && p.UserId == input.UserId)
            .Select(p => new WorkoutPlanSummaryResponse(
                p.Id,
                p.Name,
                p.Goal,
                p.CoverImageUrl,
                p.IsActive,
                p.WorkoutDays
                    .OrderBy(d => d.WeekDay)
                    .Select(d => new WorkoutDaySummaryResponse(
                        d.Id, d.WeekDay, d.Name, d.IsRest, d.CoverImageUrl, d.EstimatedDurationInSeconds, d.Exercises.Count))
                    .ToList()))
            .FirstOrDefaultAsync(ct);
        return plan ?? throw new NotFoundException("Workout plan not found");
    }
}
