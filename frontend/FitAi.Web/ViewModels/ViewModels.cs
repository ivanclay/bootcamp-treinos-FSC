using FitAi.Contracts;

namespace FitAi.Web.ViewModels;

public static class Images
{
    public const string Upper = "https://gw8hy3fdcv.ufs.sh/f/ccoBDpLoAPCO3y8pQ6GBg8iqe9pP2JrHjwd1nfKtVSQskI0v";
    public const string Lower = "https://gw8hy3fdcv.ufs.sh/f/ccoBDpLoAPCOgCHaUgNGronCvXmSzAMs1N3KgLdE5yHT6Ykj";
    public const string Login = "https://gw8hy3fdcv.ufs.sh/f/ccoBDpLoAPCOW3fJmqZe4yoUcwvRPQa8kmFprzNiC30hqftL";

    public static string Or(string? url, string fallback = Upper) => string.IsNullOrWhiteSpace(url) ? fallback : url;
}

public sealed record LoginViewModel(AuthProvidersResponse Providers, string? Error);

public sealed record HomeViewModel(HomeDataResponse Home, string FirstName, DateOnly Today);

public sealed record WorkoutPlanViewModel(WorkoutPlanResponse? Plan);

public sealed record WorkoutDayViewModel(WorkoutDayResponse Day, bool IsToday, WorkoutSessionResponse? TodaySession, bool IsActivePlan);

public sealed record CoachViewModel(bool Onboarding, bool ShowInviteCode, string? Prefill);

public sealed record StatsViewModel(StatsResponse? Stats, IReadOnlyList<HeatmapMonth> Months);

public sealed record HeatmapMonth(string Label, IReadOnlyList<IReadOnlyList<HeatmapCell>> Weeks);

/// <summary>State: "blank" (fora do mês), "none", "started" ou "done".</summary>
public sealed record HeatmapCell(string Date, string State);

public sealed record ProfileViewModel(CurrentUserResponse User, UserTrainDataResponse? TrainData);

public sealed class EditProfileViewModel
{
    public string? Name { get; set; }
    public decimal WeightKg { get; set; }
    public int HeightInCentimeters { get; set; }
    public int Age { get; set; }
    public int BodyFatPercentage { get; set; }
    public string? Error { get; set; }
}
