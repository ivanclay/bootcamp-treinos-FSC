using FitAi.Api.Data;
using FitAi.Api.Entities;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.Shared;

/// <summary>Conversões de entidades do banco para os DTOs de <c>FitAi.Contracts</c>.</summary>
public static class Mappers
{
    public static WorkoutPlanResponse ToResponse(this WorkoutPlan plan) => new(
        plan.Id,
        plan.Name,
        plan.Goal,
        plan.CoverImageUrl,
        plan.IsActive,
        plan.Source,
        plan.CreatedAt,
        plan.WorkoutDays.OrderBy(d => d.WeekDay).Select(d => d.ToDetailResponse()).ToList());

    public static WorkoutDayDetailResponse ToDetailResponse(this WorkoutDay day) => new(
        day.Id,
        day.Name,
        day.WeekDay,
        day.IsRest,
        day.EstimatedDurationInSeconds,
        day.CoverImageUrl,
        day.Exercises.OrderBy(e => e.Order).Select(e => e.ToResponse()).ToList());

    public static WorkoutExerciseResponse ToResponse(this WorkoutExercise exercise) => new(
        exercise.Id, exercise.Order, exercise.Name, exercise.Sets, exercise.Reps, exercise.RestTimeInSeconds);

    public static UserTrainDataResponse? ToTrainData(this User user) =>
        user is { WeightInGrams: { } weight, HeightInCentimeters: { } height, Age: { } age, BodyFatPercentage: { } bodyFat }
            ? new UserTrainDataResponse(user.Id, user.Name, weight, height, age, bodyFat)
            : null;

    public static async Task<CurrentUserResponse> ToCurrentUserResponseAsync(this User user, AppDbContext db, CancellationToken ct)
    {
        var teacherName = user.TeacherId is null
            ? null
            : await db.Users.Where(u => u.Id == user.TeacherId).Select(u => u.Name).FirstOrDefaultAsync(ct);
        var hasActivePlan = await db.WorkoutPlans.AnyAsync(p => p.UserId == user.Id && p.IsActive, ct);
        return new CurrentUserResponse(
            user.Id,
            user.Name,
            user.Email,
            user.Image,
            user.Role,
            user.TeacherId,
            teacherName,
            user.AllowAiWorkoutPlans,
            user.ToTrainData() is not null,
            hasActivePlan);
    }

    public static IEnumerable<WorkoutDay> ToEntities(this IEnumerable<SaveWorkoutDayRequest> days) =>
        days.Select(day => new WorkoutDay
        {
            Name = day.Name.Trim(),
            WeekDay = day.WeekDay,
            IsRest = day.IsRest,
            EstimatedDurationInSeconds = day.IsRest ? 0 : day.EstimatedDurationInSeconds,
            CoverImageUrl = string.IsNullOrWhiteSpace(day.CoverImageUrl) ? null : day.CoverImageUrl,
            Exercises = day.IsRest
                ? []
                : day.Exercises.Select(exercise => new WorkoutExercise
                {
                    Name = exercise.Name.Trim(),
                    Order = exercise.Order,
                    Sets = exercise.Sets,
                    Reps = exercise.Reps,
                    RestTimeInSeconds = exercise.RestTimeInSeconds,
                }).ToList(),
        });
}
