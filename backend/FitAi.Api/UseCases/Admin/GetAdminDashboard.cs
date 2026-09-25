using FitAi.Api.Data;
using FitAi.Api.Domain;
using FitAi.Api.Entities;
using FitAi.Api.UseCases.Shared;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.Admin;

/// <summary>Métricas do painel. Admin vê a plataforma toda; professor, só os próprios alunos.</summary>
public sealed class GetAdminDashboard(AppDbContext db, TimeProvider timeProvider)
{
    public sealed record Input(User Actor);

    public async Task<AdminDashboardResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var since7 = now.AddDays(-7);
        var today = WorkoutStreak.ToUtcDate(now);
        var firstDay = today.AddDays(-29);
        var since30 = new DateTimeOffset(firstDay.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var students = db.Users.ManagedBy(input.Actor).Where(u => u.Role == UserRole.STUDENT);
        var studentIds = students.Select(u => u.Id);
        var sessions = db.WorkoutSessions.Where(s => studentIds.Contains(s.WorkoutDay.WorkoutPlan.UserId));

        var totalStudents = await students.CountAsync(ct);
        var activeStudents = await students.CountAsync(
            u => u.WorkoutPlans.SelectMany(p => p.WorkoutDays).SelectMany(d => d.Sessions).Any(s => s.StartedAt >= since7), ct);
        var withoutPlan = await students.CountAsync(u => !u.WorkoutPlans.Any(p => p.IsActive), ct);
        var totalTeachers = input.Actor.Role == UserRole.ADMIN
            ? await db.Users.CountAsync(u => u.Role == UserRole.TEACHER, ct)
            : 0;
        var activePlans = await db.WorkoutPlans.CountAsync(p => p.IsActive && studentIds.Contains(p.UserId), ct);

        var recentSessions = await sessions
            .Where(s => s.StartedAt >= since30)
            .Select(s => new { s.StartedAt, s.CompletedAt })
            .ToListAsync(ct);
        var completed = recentSessions.Where(s => s.CompletedAt is not null).ToList();

        var byDay = completed
            .GroupBy(s => WorkoutStreak.ToUtcDate(s.StartedAt))
            .ToDictionary(g => g.Key, g => g.Count());
        var completedByDay = Enumerable.Range(0, 30)
            .Select(i => firstDay.AddDays(i))
            .Select(d => new DailyCountResponse(WorkoutStreak.ToKey(d), byDay.GetValueOrDefault(d)))
            .ToList();

        var invites = db.EmailInvites.Where(i => i.AcceptedAt == null && i.DeclinedAt == null);
        if (input.Actor.Role != UserRole.ADMIN) invites = invites.Where(i => i.TeacherId == input.Actor.Id);
        var pendingInvites = await invites.CountAsync(ct);

        var recentlyActive = await students
            .Where(u => u.WorkoutPlans.SelectMany(p => p.WorkoutDays).SelectMany(d => d.Sessions).Any())
            .OrderByDescending(u => u.WorkoutPlans.SelectMany(p => p.WorkoutDays).SelectMany(d => d.Sessions).Max(s => s.StartedAt))
            .Take(8)
            .Select(AdminProjections.UserListItem)
            .ToListAsync(ct);

        return new AdminDashboardResponse(
            totalStudents,
            activeStudents,
            withoutPlan,
            totalTeachers,
            activePlans,
            recentSessions.Count,
            completed.Count,
            recentSessions.Count > 0 ? (double)completed.Count / recentSessions.Count : 0,
            pendingInvites,
            completedByDay,
            recentlyActive);
    }
}
