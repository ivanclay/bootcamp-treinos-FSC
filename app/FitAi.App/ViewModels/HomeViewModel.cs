using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitAi.App.Services;
using FitAi.Contracts;

namespace FitAi.App.ViewModels;

public sealed record WeekDot(string Initial, Color Fill, Color Stroke, bool IsToday);

public partial class HomeViewModel(ApiClient api, AuthService auth) : BaseViewModel
{
    private TodayWorkoutDayResponse? _today;

    public ObservableCollection<WeekDot> Week { get; } = [];

    [ObservableProperty] public partial string Greeting { get; set; } = "Olá!";
    [ObservableProperty] public partial string Subtitle { get; set; } = "Bora treinar hoje?";
    [ObservableProperty] public partial string HeroImage { get; set; } = Format.Cover(null);
    [ObservableProperty] public partial int Streak { get; set; }
    [ObservableProperty] public partial bool HasPlan { get; set; }
    [ObservableProperty] public partial bool HasTraining { get; set; }
    [ObservableProperty] public partial bool IsRestDay { get; set; }
    [ObservableProperty] public partial string TodayName { get; set; } = "";
    [ObservableProperty] public partial string TodayWeekDay { get; set; } = "";
    [ObservableProperty] public partial string TodayDuration { get; set; } = "";
    [ObservableProperty] public partial string TodayExercises { get; set; } = "";
    [ObservableProperty] public partial string TodayCover { get; set; } = Format.Cover(null);

    public bool HasNoPlan => !HasPlan;

    partial void OnHasPlanChanged(bool value) => OnPropertyChanged(nameof(HasNoPlan));

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var home = await api.GetHomeAsync(today);
        var firstName = (auth.User?.Name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        Greeting = string.IsNullOrEmpty(firstName) ? "Olá!" : $"Olá, {firstName}";

        _today = home.TodayWorkoutDay;
        HasPlan = home.ActiveWorkoutPlanId is not null;
        HasTraining = _today is { IsRest: false };
        IsRestDay = _today is { IsRest: true };
        Subtitle = IsRestDay ? "Hoje é dia de descanso" : "Bora treinar hoje?";
        HeroImage = Format.Cover(_today?.CoverImageUrl);
        TodayCover = Format.Cover(_today?.CoverImageUrl);
        TodayName = _today?.Name ?? "";
        TodayWeekDay = _today is null ? "" : Format.DayName(_today.WeekDay).ToUpperInvariant();
        TodayDuration = _today is null ? "" : Format.Minutes(_today.EstimatedDurationInSeconds);
        TodayExercises = _today is null ? "" : $"{_today.ExercisesCount} exercícios";
        Streak = home.WorkoutStreak;

        var brand = Color.FromArgb("#2B54FF");
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        Week.Clear();
        for (var i = 0; i < 7; i++)
        {
            var date = monday.AddDays(i);
            var c = home.ConsistencyByDay.GetValueOrDefault(date.ToString("yyyy-MM-dd"));
            var fill = c is { WorkoutDayCompleted: true } ? brand : c is { WorkoutDayStarted: true } ? Color.FromArgb("#EEF1FF") : Colors.White;
            var stroke = c is { WorkoutDayCompleted: true } ? brand : date == today ? brand : Color.FromArgb("#D4D4D8");
            Week.Add(new WeekDot(Format.DayName(Format.WeekOrder[i])[..1], fill, stroke, date == today));
        }
    });

    [RelayCommand]
    private async Task OpenTodayAsync()
    {
        if (_today is null) { await Shell.Current.GoToAsync("//main/coach"); return; }
        await Shell.Current.GoToAsync($"workout-day?planId={_today.WorkoutPlanId}&dayId={_today.Id}");
    }

    [RelayCommand]
    private Task OpenCoachAsync() => Shell.Current.GoToAsync("//main/coach");

    [RelayCommand]
    private Task OpenStatsAsync() => Shell.Current.GoToAsync("//main/stats");

    [RelayCommand]
    private Task OpenPlanAsync() => Shell.Current.GoToAsync("//main/plan");
}
