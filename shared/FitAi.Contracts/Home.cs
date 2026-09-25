namespace FitAi.Contracts;

public sealed record HomeDataResponse(
    Guid? ActiveWorkoutPlanId,
    TodayWorkoutDayResponse? TodayWorkoutDay,
    int WorkoutStreak,
    IReadOnlyDictionary<string, DayConsistency> ConsistencyByDay);

public sealed record TodayWorkoutDayResponse(
    Guid WorkoutPlanId,
    Guid Id,
    string Name,
    bool IsRest,
    WeekDay WeekDay,
    int EstimatedDurationInSeconds,
    string? CoverImageUrl,
    int ExercisesCount);

public sealed record StatsResponse(
    int WorkoutStreak,
    IReadOnlyDictionary<string, DayConsistency> ConsistencyByDay,
    int CompletedWorkoutsCount,
    double ConclusionRate,
    long TotalTimeInSeconds);
