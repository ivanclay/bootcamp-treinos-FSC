using System.ComponentModel.DataAnnotations;

namespace FitAi.Contracts;

public sealed record WorkoutPlanResponse(
    Guid Id,
    string Name,
    WorkoutGoal? Goal,
    string? CoverImageUrl,
    bool IsActive,
    WorkoutPlanSource Source,
    DateTimeOffset CreatedAt,
    IReadOnlyList<WorkoutDayDetailResponse> WorkoutDays);

public sealed record WorkoutDayDetailResponse(
    Guid Id,
    string Name,
    WeekDay WeekDay,
    bool IsRest,
    int EstimatedDurationInSeconds,
    string? CoverImageUrl,
    IReadOnlyList<WorkoutExerciseResponse> Exercises);

public sealed record WorkoutExerciseResponse(
    Guid Id,
    int Order,
    string Name,
    int Sets,
    int Reps,
    int RestTimeInSeconds);

public sealed record WorkoutPlanSummaryResponse(
    Guid Id,
    string Name,
    WorkoutGoal? Goal,
    string? CoverImageUrl,
    bool IsActive,
    IReadOnlyList<WorkoutDaySummaryResponse> WorkoutDays);

public sealed record WorkoutDaySummaryResponse(
    Guid Id,
    WeekDay WeekDay,
    string Name,
    bool IsRest,
    string? CoverImageUrl,
    int EstimatedDurationInSeconds,
    int ExercisesCount);

public sealed record WorkoutDayResponse(
    Guid Id,
    Guid WorkoutPlanId,
    string Name,
    bool IsRest,
    string? CoverImageUrl,
    int EstimatedDurationInSeconds,
    WeekDay WeekDay,
    IReadOnlyList<WorkoutExerciseResponse> Exercises,
    IReadOnlyList<WorkoutSessionResponse> Sessions);

public sealed record WorkoutSessionResponse(
    Guid Id,
    Guid WorkoutDayId,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt);

public sealed record StartWorkoutSessionResponse(Guid UserWorkoutSessionId);

public sealed class CompleteWorkoutSessionRequest
{
    [Required]
    public DateTimeOffset? CompletedAt { get; set; }
}

/// <summary>Corpo usado para criar (aluno/IA/professor) ou substituir (professor) um plano.</summary>
public sealed class SaveWorkoutPlanRequest
{
    [Required, MinLength(1), MaxLength(120)]
    public string Name { get; set; } = "";

    public WorkoutGoal? Goal { get; set; }

    [CoverImageUrl]
    public string? CoverImageUrl { get; set; }

    [Required, MinLength(1), MaxLength(7)]
    public List<SaveWorkoutDayRequest> WorkoutDays { get; set; } = [];
}

public sealed class SaveWorkoutDayRequest
{
    [Required, MinLength(1), MaxLength(120)]
    public string Name { get; set; } = "";

    public WeekDay WeekDay { get; set; }

    public bool IsRest { get; set; }

    [Range(0, 60 * 60 * 6)]
    public int EstimatedDurationInSeconds { get; set; }

    [CoverImageUrl]
    public string? CoverImageUrl { get; set; }

    public List<SaveWorkoutExerciseRequest> Exercises { get; set; } = [];
}

public sealed class SaveWorkoutExerciseRequest
{
    [Range(0, 100)]
    public int Order { get; set; }

    [Required, MinLength(1), MaxLength(120)]
    public string Name { get; set; } = "";

    [Range(1, 50)]
    public int Sets { get; set; }

    [Range(1, 500)]
    public int Reps { get; set; }

    [Range(0, 60 * 30)]
    public int RestTimeInSeconds { get; set; }
}
