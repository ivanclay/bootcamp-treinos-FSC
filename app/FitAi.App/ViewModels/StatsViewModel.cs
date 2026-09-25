using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitAi.App.Services;
using FitAi.Contracts;

namespace FitAi.App.ViewModels;

public sealed record HeatCell(Color Fill);

public partial class StatsViewModel(ApiClient api) : BaseViewModel
{
    private const int Weeks = 20;

    /// <summary>Células em ordem de coluna (semana) — o CollectionView usa GridItemsLayout horizontal com 7 linhas.</summary>
    public ObservableCollection<HeatCell> Cells { get; } = [];

    [ObservableProperty] public partial string StreakText { get; set; } = "0 dias";
    [ObservableProperty] public partial bool HasStreak { get; set; }
    [ObservableProperty] public partial bool HasStats { get; set; }
    [ObservableProperty] public partial string Completed { get; set; } = "0";
    [ObservableProperty] public partial string Rate { get; set; } = "0%";
    [ObservableProperty] public partial string TotalTime { get; set; } = "0m";

    public bool HasNoStats => !HasStats;
    public bool HasNoStreak => !HasStreak;

    partial void OnHasStatsChanged(bool value) => OnPropertyChanged(nameof(HasNoStats));
    partial void OnHasStreakChanged(bool value) => OnPropertyChanged(nameof(HasNoStreak));

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var start = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)).AddDays(-7 * (Weeks - 1));

        StatsResponse? stats = null;
        try { stats = await api.GetStatsAsync(start, today); }
        catch (ApiException e) when (e.Code == ErrorCodes.NotFound) { }

        HasStats = stats is not null;
        var streak = stats?.WorkoutStreak ?? 0;
        HasStreak = streak > 0;
        StreakText = streak == 1 ? "1 dia" : $"{streak} dias";
        Completed = (stats?.CompletedWorkoutsCount ?? 0).ToString();
        Rate = Format.Percent(stats?.ConclusionRate ?? 0);
        TotalTime = Format.TotalTime(stats?.TotalTimeInSeconds ?? 0);

        Cells.Clear();
        for (var day = start; day < start.AddDays(7 * Weeks); day = day.AddDays(1))
        {
            var c = stats?.ConsistencyByDay.GetValueOrDefault(day.ToString("yyyy-MM-dd"));
            var fill = day > today ? Colors.Transparent
                : c is { WorkoutDayCompleted: true } ? Color.FromArgb("#2B54FF")
                : c is { WorkoutDayStarted: true } ? Color.FromArgb("#C7D2FE")
                : Color.FromArgb("#E4E4E7");
            Cells.Add(new HeatCell(fill));
        }
    });

    [RelayCommand]
    private Task OpenCoachAsync() => Shell.Current.GoToAsync("//main/coach");
}
