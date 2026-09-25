using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitAi.App.Services;
using FitAi.Contracts;

namespace FitAi.App.ViewModels;

public sealed record ExerciseItem(string Name, string Sets, string Reps, string Rest);

public partial class WorkoutDayViewModel(ApiClient api) : BaseViewModel, IQueryAttributable
{
    private Guid _planId;
    private Guid _dayId;
    private Guid? _sessionId;

    public ObservableCollection<ExerciseItem> Exercises { get; } = [];

    [ObservableProperty] public partial string Title { get; set; } = "Treino";
    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string WeekDay { get; set; } = "";
    [ObservableProperty] public partial string Duration { get; set; } = "";
    [ObservableProperty] public partial string ExercisesCount { get; set; } = "";
    [ObservableProperty] public partial string Cover { get; set; } = Format.Cover(null);
    [ObservableProperty] public partial bool CanStart { get; set; }
    [ObservableProperty] public partial bool CanComplete { get; set; }
    [ObservableProperty] public partial bool IsCompleted { get; set; }
    [ObservableProperty] public partial string? Message { get; set; }

    public bool HasMessage => Message is not null;

    partial void OnMessageChanged(string? value) => OnPropertyChanged(nameof(HasMessage));

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _planId = Guid.Parse(query["planId"].ToString()!);
        _dayId = Guid.Parse(query["dayId"].ToString()!);
    }

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        var day = await api.GetWorkoutDayAsync(_planId, _dayId);
        var activePlans = await api.ListActivePlansAsync();
        var today = DateOnly.FromDateTime(DateTime.Now);
        var session = day.Sessions.FirstOrDefault(s => DateOnly.FromDateTime(s.StartedAt.LocalDateTime) == today);

        Title = Format.WeekDayOf(today) == day.WeekDay ? "Treino de Hoje" : Format.DayName(day.WeekDay);
        Name = day.Name;
        WeekDay = Format.DayName(day.WeekDay).ToUpperInvariant();
        Duration = Format.Minutes(day.EstimatedDurationInSeconds);
        ExercisesCount = $"{day.Exercises.Count} exercícios";
        Cover = Format.Cover(day.CoverImageUrl);
        _sessionId = session?.Id;
        IsCompleted = session?.CompletedAt is not null;
        CanComplete = session is { CompletedAt: null };
        CanStart = session is null && !day.IsRest && activePlans.Any(p => p.Id == _planId);

        Exercises.Clear();
        foreach (var e in day.Exercises)
        {
            Exercises.Add(new ExerciseItem(e.Name, $"{e.Sets} SÉRIES", $"{e.Reps} REPS", Format.Rest(e.RestTimeInSeconds)));
        }
    });

    [RelayCommand]
    private Task StartAsync() => RunAsync(async () =>
    {
        var result = await api.StartSessionAsync(_planId, _dayId);
        _sessionId = result.UserWorkoutSessionId;
        CanStart = false;
        CanComplete = true;
        Message = "Treino iniciado. Bom treino! 💪";
    });

    [RelayCommand]
    private Task CompleteAsync() => RunAsync(async () =>
    {
        if (_sessionId is not { } sessionId) return;
        await api.CompleteSessionAsync(_planId, _dayId, sessionId);
        CanComplete = false;
        IsCompleted = true;
        Message = "Treino concluído! 🔥";
    });

    [RelayCommand]
    private async Task AskCoachAsync(ExerciseItem? exercise)
    {
        if (exercise is null) return;
        CoachViewModel.PendingQuestion = $"Como executar corretamente o exercício {exercise.Name}? Quais são os erros comuns?";
        await Shell.Current.GoToAsync("//main/coach");
    }
}
