using FitAi.Api.Data;
using FitAi.Api.Domain;
using FitAi.Api.Entities;
using FitAi.Api.Errors;
using FitAi.Api.UseCases.Shared;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.Admin;

public sealed class GetUserDetail(AppDbContext db, TimeProvider timeProvider)
{
    public sealed record Input(User Actor, string UserId);

    public async Task<AdminUserDetailResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var item = await db.Users.AsNoTracking().ManagedBy(input.Actor)
            .Where(u => u.Id == input.UserId)
            .Select(AdminProjections.UserListItem)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("User not found");

        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == input.UserId, ct);
        var plans = await db.WorkoutPlans.AsNoTracking()
            .Where(p => p.UserId == input.UserId)
            .Include(p => p.WorkoutDays).ThenInclude(d => d.Exercises)
            .Include(p => p.WorkoutDays).ThenInclude(d => d.Sessions)
            .AsSplitQuery()
            .OrderByDescending(p => p.IsActive).ThenByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        var now = timeProvider.GetUtcNow();
        var today = WorkoutStreak.ToUtcDate(now);
        var activePlan = plans.FirstOrDefault(p => p.IsActive);
        var allSessions = plans
            .SelectMany(p => p.WorkoutDays.SelectMany(d => d.Sessions.Select(s => (Day: d, Session: s))))
            .ToList();

        var streak = activePlan is null
            ? 0
            : WorkoutStreak.Calculate(
                activePlan.WorkoutDays.Select(d => (d.WeekDay, d.IsRest)),
                WorkoutStreak.GetCompletedDates(activePlan.WorkoutDays.SelectMany(d => d.Sessions).Select(s => (s.StartedAt, s.CompletedAt))),
                today,
                WorkoutStreak.ToUtcDate(activePlan.CreatedAt));

        var completedLast30 = allSessions.Count(x => x.Session.CompletedAt is not null && x.Session.StartedAt >= now.AddDays(-30));
        var recent = allSessions
            .OrderByDescending(x => x.Session.StartedAt)
            .Take(20)
            .Select(x => new AdminSessionResponse(x.Session.Id, x.Day.Name, x.Session.StartedAt, x.Session.CompletedAt))
            .ToList();

        return new AdminUserDetailResponse(
            item,
            user.ToTrainData(),
            streak,
            completedLast30,
            plans.Select(p => p.ToResponse()).ToList(),
            recent);
    }
}
