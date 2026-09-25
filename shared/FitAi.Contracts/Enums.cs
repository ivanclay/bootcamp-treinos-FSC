using System.Text.Json.Serialization;

namespace FitAi.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<WeekDay>))]
public enum WeekDay
{
    MONDAY,
    TUESDAY,
    WEDNESDAY,
    THURSDAY,
    FRIDAY,
    SATURDAY,
    SUNDAY,
}

[JsonConverter(typeof(JsonStringEnumConverter<WorkoutGoal>))]
public enum WorkoutGoal
{
    HYPERTROPHY,
    STRENGTH,
    HYPERTROPHY_AND_STRENGTH,
    WEIGHT_LOSS,
    CONDITIONING,
    HEALTH,
}

[JsonConverter(typeof(JsonStringEnumConverter<UserRole>))]
public enum UserRole
{
    STUDENT,
    TEACHER,
    ADMIN,
}

/// <summary>Quem montou o plano de treino.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WorkoutPlanSource>))]
public enum WorkoutPlanSource
{
    AI,
    TEACHER,
}
