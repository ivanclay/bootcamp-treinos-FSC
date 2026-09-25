using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitAi.App.Services;
using FitAi.Contracts;

namespace FitAi.App.ViewModels;

public sealed record PlanDayItem(Guid PlanId, Guid DayId, string WeekDay, string Name, bool IsRest, string Duration, string Exercises, string Cover)
{
    public bool IsTraining => !IsRest;
}

public partial class PlanViewModel(ApiClient api) : BaseViewModel
{
    public ObservableCollection<PlanDayItem> Days { get; } = [];

    [ObservableProperty] public partial bool HasPlan { get; set; }
    [ObservableProperty] public partial string PlanName { get; set; } = "";
    [ObservableProperty] public partial string Goal { get; set; } = "";
    [ObservableProperty] public partial string Cover { get; set; } = Format.Cover(null);
    [ObservableProperty] public partial bool ByTeacher { get; set; }

    public bool HasNoPlan => !HasPlan;

    partial void OnHasPlanChanged(bool value) => OnPropertyChanged(nameof(HasNoPlan));

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        var plan = (await api.ListActivePlansAsync()).FirstOrDefault();
        HasPlan = plan is not null;
        Days.Clear();
        if (plan is null) return;

        PlanName = plan.Name;
        Goal = Format.Goal(plan.Goal).ToUpperInvariant();
        Cover = Format.Cover(plan.CoverImageUrl);
        ByTeacher = plan.Source == WorkoutPlanSource.TEACHER;
        foreach (var weekDay in Format.WeekOrder)
        {
            var day = plan.WorkoutDays.FirstOrDefault(d => d.WeekDay == weekDay);
            if (day is null) continue;
            Days.Add(new PlanDayItem(
                plan.Id, day.Id, Format.DayName(day.WeekDay).ToUpperInvariant(), day.IsRest ? "Descanso" : day.Name, day.IsRest,
                Format.Minutes(day.EstimatedDurationInSeconds), $"{day.Exercises.Count} exercícios", Format.Cover(day.CoverImageUrl)));
        }
    });

    [RelayCommand]
    private async Task OpenDayAsync(PlanDayItem? day)
    {
        if (day is null || day.IsRest) return;
        await Shell.Current.GoToAsync($"workout-day?planId={day.PlanId}&dayId={day.DayId}");
    }

    [RelayCommand]
    private Task OpenCoachAsync() => Shell.Current.GoToAsync("//main/coach");
}
