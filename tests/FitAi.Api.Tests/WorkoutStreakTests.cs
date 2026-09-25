using FitAi.Api.Domain;
using FitAi.Contracts;

namespace FitAi.Api.Tests;

public class WorkoutStreakTests
{
    // 2026-09-21 é uma segunda-feira.
    private static readonly DateOnly Monday = new(2026, 9, 21);

    private static readonly (WeekDay, bool)[] FullWeek =
    [
        (WeekDay.MONDAY, false), (WeekDay.TUESDAY, false), (WeekDay.WEDNESDAY, true), (WeekDay.THURSDAY, false),
        (WeekDay.FRIDAY, false), (WeekDay.SATURDAY, true), (WeekDay.SUNDAY, true),
    ];

    [Fact]
    public void GetWeekDay_MapsCalendarDays()
    {
        Assert.Equal(WeekDay.MONDAY, WorkoutStreak.GetWeekDay(Monday));
        Assert.Equal(WeekDay.SUNDAY, WorkoutStreak.GetWeekDay(Monday.AddDays(-1)));
    }

    [Fact]
    public void Today_NotCompleted_DoesNotBreakStreak()
    {
        var completed = new HashSet<DateOnly> { Monday.AddDays(-3) /* sexta */ };
        // Referência: segunda (hoje, sem treino). Domingo e sábado são descanso, sexta concluída.
        var streak = WorkoutStreak.Calculate(FullWeek, completed, Monday, Monday.AddDays(-30));
        Assert.Equal(3, streak);
    }

    [Fact]
    public void Today_Completed_Counts()
    {
        var completed = new HashSet<DateOnly> { Monday, Monday.AddDays(-3) };
        Assert.Equal(4, WorkoutStreak.Calculate(FullWeek, completed, Monday, Monday.AddDays(-30)));
    }

    [Fact]
    public void MissedTrainingDay_BreaksStreak()
    {
        // Quinta (-4) sem treino: conta sexta(-3), sábado(-2), domingo(-1) e para na quinta.
        var completed = new HashSet<DateOnly> { Monday.AddDays(-3) };
        Assert.Equal(3, WorkoutStreak.Calculate(FullWeek, completed, Monday, Monday.AddDays(-30)));
    }

    [Fact]
    public void StopsAtPlanCreation()
    {
        var streak = WorkoutStreak.Calculate(FullWeek, new HashSet<DateOnly>(), Monday.AddDays(-1), Monday.AddDays(-2));
        Assert.Equal(2, streak); // sábado e domingo de descanso
    }

    [Fact]
    public void DaysOutsideThePlan_AreSkipped()
    {
        (WeekDay, bool)[] plan = [(WeekDay.MONDAY, false), (WeekDay.WEDNESDAY, false)];
        var completed = new HashSet<DateOnly> { Monday, Monday.AddDays(-5), Monday.AddDays(-7) };
        Assert.Equal(3, WorkoutStreak.Calculate(plan, completed, Monday, Monday.AddDays(-60)));
    }

    [Fact]
    public void GetCompletedDates_UsesUtcStartDate_AndIgnoresUnfinished()
    {
        var start = new DateTimeOffset(2026, 9, 21, 23, 30, 0, TimeSpan.FromHours(-3)); // 22/09 em UTC
        var dates = WorkoutStreak.GetCompletedDates([(start, start.AddHours(1)), (start.AddDays(1), null)]);
        Assert.Equal([new DateOnly(2026, 9, 22)], dates);
    }
}
