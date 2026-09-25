using FitAi.Api.Errors;
using FitAi.Api.UseCases.WorkoutPlans;
using FitAi.Contracts;

namespace FitAi.Api.Tests;

public class WorkoutPlanValidatorTests
{
    private static SaveWorkoutDayRequest TrainingDay(WeekDay weekDay) => new()
    {
        Name = "Superiores",
        WeekDay = weekDay,
        EstimatedDurationInSeconds = 2700,
        Exercises = [new SaveWorkoutExerciseRequest { Order = 0, Name = "Supino", Sets = 3, Reps = 12, RestTimeInSeconds = 60 }],
    };

    [Fact]
    public void ValidPlan_Passes()
    {
        var plan = new SaveWorkoutPlanRequest
        {
            Name = "Plano",
            WorkoutDays = [TrainingDay(WeekDay.MONDAY), new() { Name = "Descanso", WeekDay = WeekDay.TUESDAY, IsRest = true }],
        };
        WorkoutPlanValidator.Validate(plan);
    }

    [Fact]
    public void DuplicatedWeekDay_Fails()
    {
        var plan = new SaveWorkoutPlanRequest { Name = "Plano", WorkoutDays = [TrainingDay(WeekDay.MONDAY), TrainingDay(WeekDay.MONDAY)] };
        Assert.Throws<ValidationException>(() => WorkoutPlanValidator.Validate(plan));
    }

    [Fact]
    public void TrainingDayWithoutExercises_Fails()
    {
        var day = TrainingDay(WeekDay.MONDAY);
        day.Exercises.Clear();
        Assert.Throws<ValidationException>(() => WorkoutPlanValidator.Validate(new SaveWorkoutPlanRequest { Name = "P", WorkoutDays = [day] }));
    }
}
