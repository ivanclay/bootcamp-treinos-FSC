using FitAi.Api.Data;
using FitAi.Api.Domain;
using FitAi.Api.Errors;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.Home;

public sealed class GetStats(AppDbContext db, TimeProvider timeProvider)
{
    public sealed record Input(string UserId, DateOnly From, DateOnly To);

    public async Task<StatsResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        if (input.To < input.From) throw new ValidationException("'to' must be on or after 'from'");

        var plan = await db.WorkoutPlans.AsNoTracking()
            .Include(p => p.WorkoutDays).ThenInclude(d => d.Sessions)
            .FirstOrDefaultAsync(p => p.UserId == input.UserId && p.IsActive, ct)
            ?? throw new NotFoundException("Active workout plan not found");

        var allSessions = plan.WorkoutDays.SelectMany(d => d.Sessions).ToList();
        var sessions = allSessions
            .Where(s => WorkoutStreak.ToUtcDate(s.StartedAt) is var date && date >= input.From && date <= input.To)
            .ToList();

        var consistency = sessions
            .GroupBy(s => WorkoutStreak.ToKey(WorkoutStreak.ToUtcDate(s.StartedAt)))
            .OrderBy(g => g.Key)
            .ToDictionary(
                g => g.Key,
                g => new DayConsistency(WorkoutDayCompleted: g.Any(s => s.CompletedAt is not null), WorkoutDayStarted: true));

        var completed = sessions.Where(s => s.CompletedAt is not null).ToList();
        var totalSeconds = completed.Sum(s => (long)(s.CompletedAt!.Value - s.StartedAt).TotalSeconds);

        var today = WorkoutStreak.ToUtcDate(timeProvider.GetUtcNow());
        var streak = WorkoutStreak.Calculate(
            plan.WorkoutDays.Select(d => (d.WeekDay, d.IsRest)),
            WorkoutStreak.GetCompletedDates(allSessions.Select(s => (s.StartedAt, s.CompletedAt))),
            input.To > today ? today : input.To,
            WorkoutStreak.ToUtcDate(plan.CreatedAt));

        return new StatsResponse(
            streak,
            consistency,
            completed.Count,
            sessions.Count > 0 ? (double)completed.Count / sessions.Count : 0,
            totalSeconds);
    }
}
