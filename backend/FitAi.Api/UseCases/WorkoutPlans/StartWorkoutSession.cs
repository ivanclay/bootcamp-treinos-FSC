using FitAi.Api.Data;
using FitAi.Api.Domain;
using FitAi.Api.Entities;
using FitAi.Api.Errors;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.WorkoutPlans;

public sealed class StartWorkoutSession(AppDbContext db, TimeProvider timeProvider)
{
    public sealed record Input(string UserId, Guid WorkoutPlanId, Guid WorkoutDayId);

    public async Task<StartWorkoutSessionResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var plan = await db.WorkoutPlans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == input.WorkoutPlanId && p.UserId == input.UserId, ct)
            ?? throw new NotFoundException("Workout plan not found");
        if (!plan.IsActive) throw new WorkoutPlanNotActiveException("Workout plan is not active");

        var day = await db.WorkoutDays.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == input.WorkoutDayId && d.WorkoutPlanId == input.WorkoutPlanId, ct)
            ?? throw new NotFoundException("Workout day not found");
        if (day.IsRest) throw new ValidationException("Dias de descanso não podem ser iniciados");

        // Um treino por dia de calendário (UTC): o mesmo dia do plano pode ser feito de novo na semana seguinte.
        var now = timeProvider.GetUtcNow();
        var todayStart = new DateTimeOffset(WorkoutStreak.ToUtcDate(now).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var alreadyStarted = await db.WorkoutSessions.AnyAsync(
            s => s.WorkoutDayId == day.Id && s.StartedAt >= todayStart && s.StartedAt < todayStart.AddDays(1), ct);
        if (alreadyStarted) throw new SessionAlreadyStartedException("A session has already been started for this day");

        var session = new WorkoutSession { WorkoutDayId = day.Id, StartedAt = now };
        db.WorkoutSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return new StartWorkoutSessionResponse(session.Id);
    }
}
