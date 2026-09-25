using FitAi.Contracts;

namespace FitAi.Web.ViewModels;

/// <summary>
/// Fotos de capa. Ficam na API (wwwroot/covers) e são referenciadas por caminho relativo;
/// aqui viram URL absoluta usando Api:PublicUrl (definido no Program.cs).
/// </summary>
public static class Images
{
    public const string Upper = "/covers/upper-1.jpg";
    public const string Login = "/covers/login.jpg";
    public const string Home = "/covers/home.jpg";
    public const string Plan = "/covers/plan.jpg";

    public static readonly (string Label, string Path)[] Suggestions =
    [
        ("Superior 1 — supino", "/covers/upper-1.jpg"),
        ("Superior 2 — remada", "/covers/upper-2.jpg"),
        ("Superior 3 — desenvolvimento", "/covers/upper-3.jpg"),
        ("Superior 4 — supino inclinado", "/covers/upper-4.jpg"),
        ("Inferior 1 — agachamento", "/covers/lower-1.jpg"),
        ("Inferior 2 — leg press", "/covers/lower-2.jpg"),
        ("Inferior 3 — avanço", "/covers/lower-3.jpg"),
        ("Inferior 4 — stiff", "/covers/lower-4.jpg"),
    ];

    public static string ApiPublicUrl { get; set; } = "http://localhost:8080";

    public static string Resolve(string path) => path.StartsWith('/') ? ApiPublicUrl.TrimEnd('/') + path : path;

    public static string Or(string? url, string fallback = Upper) => Resolve(string.IsNullOrWhiteSpace(url) ? fallback : url);
}

public sealed record LoginViewModel(AuthProvidersResponse Providers, string? Error);

public sealed record HomeViewModel(HomeDataResponse Home, string FirstName, DateOnly Today, int PendingInvites = 0);

public sealed record WorkoutPlanViewModel(WorkoutPlanResponse? Plan);

public sealed record WorkoutDayViewModel(WorkoutDayResponse Day, bool IsToday, WorkoutSessionResponse? TodaySession, bool IsActivePlan);

public sealed record CoachViewModel(bool Onboarding, bool ShowInviteCode, string? Prefill);

public sealed record StatsViewModel(StatsResponse? Stats, IReadOnlyList<HeatmapMonth> Months);

public sealed record HeatmapMonth(string Label, IReadOnlyList<IReadOnlyList<HeatmapCell>> Weeks);

/// <summary>State: "blank" (fora do mês), "none", "started" ou "done".</summary>
public sealed record HeatmapCell(string Date, string State);

public sealed record ProfileViewModel(CurrentUserResponse User, UserTrainDataResponse? TrainData, IReadOnlyList<PendingInviteResponse> PendingInvites, MyPlanResponse Plan);

public sealed class EditProfileViewModel
{
    public string? Name { get; set; }
    public decimal WeightKg { get; set; }
    public int HeightInCentimeters { get; set; }
    public int Age { get; set; }
    public int BodyFatPercentage { get; set; }
    public string? Error { get; set; }
}
