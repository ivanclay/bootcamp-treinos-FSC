using FitAi.Api.Data;
using FitAi.Api.Errors;
using FitAi.Api.UseCases.Shared;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.WorkoutPlans;

public sealed class GetWorkoutDay(AppDbContext db)
{
    public sealed record Input(string UserId, Guid WorkoutPlanId, Guid WorkoutDayId);

    public async Task<WorkoutDayResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var planExists = await db.WorkoutPlans.AnyAsync(p => p.Id == input.WorkoutPlanId && p.UserId == input.UserId, ct);
        if (!planExists) throw new NotFoundException("Workout plan not found");

        var day = await db.WorkoutDays.AsNoTracking()
            .Include(d => d.Exercises)
            .Include(d => d.Sessions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(d => d.Id == input.WorkoutDayId && d.WorkoutPlanId == input.WorkoutPlanId, ct)
            ?? throw new NotFoundException("Workout day not found");

        return new WorkoutDayResponse(
            day.Id,
            day.WorkoutPlanId,
            day.Name,
            day.IsRest,
            day.CoverImageUrl,
            day.EstimatedDurationInSeconds,
            day.WeekDay,
            day.Exercises.OrderBy(e => e.Order).Select(e => e.ToResponse()).ToList(),
            day.Sessions.OrderByDescending(s => s.StartedAt)
                .Select(s => new WorkoutSessionResponse(s.Id, s.WorkoutDayId, s.StartedAt, s.CompletedAt))
                .ToList());
    }
}
