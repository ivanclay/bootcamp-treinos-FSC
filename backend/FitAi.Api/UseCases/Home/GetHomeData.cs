using FitAi.Api.Data;
using FitAi.Api.Domain;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.Home;

public sealed class GetHomeData(AppDbContext db)
{
    public sealed record Input(string UserId, DateOnly Date);

    public async Task<HomeDataResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var plan = await db.WorkoutPlans.AsNoTracking()
            .Include(p => p.WorkoutDays).ThenInclude(d => d.Exercises)
            .Include(p => p.WorkoutDays).ThenInclude(d => d.Sessions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.UserId == input.UserId && p.IsActive, ct);

        var todayWeekDay = WorkoutStreak.GetWeekDay(input.Date);
        var today = plan?.WorkoutDays.FirstOrDefault(d => d.WeekDay == todayWeekDay);

        // Semana de domingo a sábado que contém a data.
        var weekStart = input.Date.AddDays(-(int)input.Date.DayOfWeek);
        var sessions = plan?.WorkoutDays.SelectMany(d => d.Sessions).ToList() ?? [];

        var consistency = new Dictionary<string, DayConsistency>();
        for (var i = 0; i < 7; i++)
        {
            var day = weekStart.AddDays(i);
            var daySessions = sessions.Where(s => WorkoutStreak.ToUtcDate(s.StartedAt) == day).ToList();
            consistency[WorkoutStreak.ToKey(day)] = new DayConsistency(
                WorkoutDayCompleted: daySessions.Any(s => s.CompletedAt is not null),
                WorkoutDayStarted: daySessions.Count > 0);
        }

        var streak = plan is null
            ? 0
            : WorkoutStreak.Calculate(
                plan.WorkoutDays.Select(d => (d.WeekDay, d.IsRest)),
                WorkoutStreak.GetCompletedDates(sessions.Select(s => (s.StartedAt, s.CompletedAt))),
                input.Date,
                WorkoutStreak.ToUtcDate(plan.CreatedAt));

        return new HomeDataResponse(
            plan?.Id,
            plan is not null && today is not null
                ? new TodayWorkoutDayResponse(
                    plan.Id,
                    today.Id,
                    today.Name,
                    today.IsRest,
                    today.WeekDay,
                    today.EstimatedDurationInSeconds,
                    today.CoverImageUrl,
                    today.Exercises.Count)
                : null,
            streak,
            consistency);
    }
}
