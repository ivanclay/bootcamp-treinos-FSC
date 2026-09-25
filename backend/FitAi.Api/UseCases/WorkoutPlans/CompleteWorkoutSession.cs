using FitAi.Api.Data;
using FitAi.Api.Errors;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.WorkoutPlans;

public sealed class CompleteWorkoutSession(AppDbContext db)
{
    public sealed record Input(string UserId, Guid WorkoutPlanId, Guid WorkoutDayId, Guid SessionId, DateTimeOffset CompletedAt);

    public async Task<WorkoutSessionResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var session = await db.WorkoutSessions
            .Include(s => s.WorkoutDay).ThenInclude(d => d.WorkoutPlan)
            .FirstOrDefaultAsync(s => s.Id == input.SessionId && s.WorkoutDayId == input.WorkoutDayId, ct);
        if (session is null
            || session.WorkoutDay.WorkoutPlanId != input.WorkoutPlanId
            || session.WorkoutDay.WorkoutPlan.UserId != input.UserId)
        {
            throw new NotFoundException("Workout session not found");
        }
        if (input.CompletedAt < session.StartedAt)
        {
            throw new ValidationException("A conclusão deve ser depois do início do treino");
        }

        session.CompletedAt = input.CompletedAt.ToUniversalTime();
        await db.SaveChangesAsync(ct);
        return new WorkoutSessionResponse(session.Id, session.WorkoutDayId, session.StartedAt, session.CompletedAt);
    }
}
