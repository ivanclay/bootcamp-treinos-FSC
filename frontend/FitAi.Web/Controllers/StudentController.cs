using FitAi.Contracts;
using FitAi.Web.Infrastructure;
using FitAi.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitAi.Web.Controllers;

/// <summary>Telas do aluno: Home, Plano, Treino do dia, Evolução.</summary>
[Authorize]
public sealed class StudentController(ApiClient api, AppClock clock) : Controller
{
    [HttpGet("/")]
    public async Task<IActionResult> Home(CancellationToken ct)
    {
        var home = await api.GetHomeAsync(clock.Today, ct);
        var invites = await api.ListPendingInvitesAsync(ct);
        ViewData["Nav"] = "home";
        return View(new HomeViewModel(home, User.FirstName(), clock.Today, invites.Count));
    }

    [HttpGet("/plano")]
    public async Task<IActionResult> Plan(CancellationToken ct)
    {
        var plans = await api.ListWorkoutPlansAsync(active: true, ct);
        ViewData["Nav"] = "plan";
        return View(new WorkoutPlanViewModel(plans.FirstOrDefault()));
    }

    [HttpGet("/treino/{planId:guid}/{dayId:guid}")]
    public async Task<IActionResult> Day(Guid planId, Guid dayId, CancellationToken ct)
    {
        var day = await api.GetWorkoutDayAsync(planId, dayId, ct);
        var activePlans = await api.ListWorkoutPlansAsync(active: true, ct);
        var today = clock.Today;
        var todaySession = day.Sessions.FirstOrDefault(s => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(s.StartedAt, clock.TimeZone).DateTime) == today);
        var isToday = day.WeekDay == Format.WeekOrder[((int)today.DayOfWeek + 6) % 7];
        ViewData["Nav"] = "plan";
        return View(new WorkoutDayViewModel(day, isToday, todaySession, activePlans.Any(p => p.Id == planId)));
    }

    [HttpPost("/treino/{planId:guid}/{dayId:guid}/iniciar")]
    public async Task<IActionResult> Start(Guid planId, Guid dayId, CancellationToken ct)
    {
        try { await api.StartSessionAsync(planId, dayId, ct); }
        catch (ApiException e) when (e.Code == ErrorCodes.SessionAlreadyStarted) { }
        return Redirect($"/treino/{planId}/{dayId}");
    }

    [HttpPost("/treino/{planId:guid}/{dayId:guid}/concluir/{sessionId:guid}")]
    public async Task<IActionResult> Complete(Guid planId, Guid dayId, Guid sessionId, CancellationToken ct)
    {
        await api.CompleteSessionAsync(planId, dayId, sessionId, ct);
        TempData["Flash"] = "Treino concluído! 💪";
        return Redirect($"/treino/{planId}/{dayId}");
    }

    [HttpGet("/evolucao")]
    public async Task<IActionResult> Stats(CancellationToken ct)
    {
        var today = clock.Today;
        var firstMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(-4);
        StatsResponse? stats = null;
        try { stats = await api.GetStatsAsync(firstMonth, today, ct); }
        catch (ApiException e) when (e.Code == ErrorCodes.NotFound) { }

        ViewData["Nav"] = "stats";
        return View(new StatsViewModel(stats, BuildHeatmap(firstMonth, today, stats)));
    }

    /// <summary>Um bloco por mês; colunas são semanas (segunda a domingo).</summary>
    private static List<HeatmapMonth> BuildHeatmap(DateOnly firstMonth, DateOnly today, StatsResponse? stats)
    {
        var months = new List<HeatmapMonth>();
        for (var month = firstMonth; month <= today; month = month.AddMonths(1))
        {
            var weeks = new List<IReadOnlyList<HeatmapCell>>();
            var first = month;
            var last = month.AddMonths(1).AddDays(-1);
            var cursor = first.AddDays(-(((int)first.DayOfWeek + 6) % 7));
            while (cursor <= last)
            {
                var week = new List<HeatmapCell>();
                for (var i = 0; i < 7; i++, cursor = cursor.AddDays(1))
                {
                    var key = cursor.ToString("yyyy-MM-dd");
                    if (cursor < first || cursor > last) { week.Add(new HeatmapCell(key, "blank")); continue; }
                    var state = stats?.ConsistencyByDay.GetValueOrDefault(key) switch
                    {
                        { WorkoutDayCompleted: true } => "done",
                        { WorkoutDayStarted: true } => "started",
                        _ => "none",
                    };
                    week.Add(new HeatmapCell(key, state));
                }
                weeks.Add(week);
            }
            months.Add(new HeatmapMonth(Format.MonthShort(month.Month), weeks));
        }
        return months;
    }
}
