using FitAi.Contracts;

namespace FitAi.Api.Domain;

/// <summary>Cálculo da sequência (🔥) — função pura, sem acesso ao banco. Datas em UTC.</summary>
public static class WorkoutStreak
{
    private const int MaxLookbackInDays = 365;

    public static WeekDay GetWeekDay(DateOnly date) => date.DayOfWeek switch
    {
        DayOfWeek.Sunday => WeekDay.SUNDAY,
        DayOfWeek.Monday => WeekDay.MONDAY,
        DayOfWeek.Tuesday => WeekDay.TUESDAY,
        DayOfWeek.Wednesday => WeekDay.WEDNESDAY,
        DayOfWeek.Thursday => WeekDay.THURSDAY,
        DayOfWeek.Friday => WeekDay.FRIDAY,
        _ => WeekDay.SATURDAY,
    };

    public static DateOnly ToUtcDate(DateTimeOffset value) => DateOnly.FromDateTime(value.UtcDateTime);

    public static string ToKey(DateOnly date) => date.ToString("yyyy-MM-dd");

    public static HashSet<DateOnly> GetCompletedDates(IEnumerable<(DateTimeOffset StartedAt, DateTimeOffset? CompletedAt)> sessions) =>
        sessions.Where(s => s.CompletedAt is not null).Select(s => ToUtcDate(s.StartedAt)).ToHashSet();

    /// <summary>
    /// Conta, da data de referência para trás, os dias seguidos cumpridos. Dia de descanso conta;
    /// dia fora do plano é pulado; o dia de referência (hoje) não quebra a sequência.
    /// </summary>
    public static int Calculate(
        IEnumerable<(WeekDay WeekDay, bool IsRest)> workoutDays,
        IReadOnlySet<DateOnly> completedDates,
        DateOnly referenceDate,
        DateOnly planCreatedAt)
    {
        var days = workoutDays.ToList();
        var planWeekDays = days.Select(d => d.WeekDay).ToHashSet();
        var restWeekDays = days.Where(d => d.IsRest).Select(d => d.WeekDay).ToHashSet();

        var streak = 0;
        for (var offset = 0; offset < MaxLookbackInDays; offset++)
        {
            var day = referenceDate.AddDays(-offset);
            if (day < planCreatedAt) break;

            var weekDay = GetWeekDay(day);
            if (!planWeekDays.Contains(weekDay)) continue;

            if (restWeekDays.Contains(weekDay) || completedDates.Contains(day))
            {
                streak++;
                continue;
            }

            // O dia de referência (hoje) ainda pode ser concluído: não quebra a sequência.
            if (offset == 0) continue;

            break;
        }

        return streak;
    }
}
