using System.Globalization;
using FitAi.Contracts;

namespace FitAi.App.Services;

public static class Format
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static readonly WeekDay[] WeekOrder =
        [WeekDay.MONDAY, WeekDay.TUESDAY, WeekDay.WEDNESDAY, WeekDay.THURSDAY, WeekDay.FRIDAY, WeekDay.SATURDAY, WeekDay.SUNDAY];

    public static string DayName(WeekDay day) => day switch
    {
        WeekDay.MONDAY => "Segunda",
        WeekDay.TUESDAY => "Terça",
        WeekDay.WEDNESDAY => "Quarta",
        WeekDay.THURSDAY => "Quinta",
        WeekDay.FRIDAY => "Sexta",
        WeekDay.SATURDAY => "Sábado",
        _ => "Domingo",
    };

    public static WeekDay WeekDayOf(DateOnly date) => WeekOrder[((int)date.DayOfWeek + 6) % 7];

    public static string Goal(WorkoutGoal? goal) => goal switch
    {
        WorkoutGoal.HYPERTROPHY => "Hipertrofia",
        WorkoutGoal.STRENGTH => "Força",
        WorkoutGoal.HYPERTROPHY_AND_STRENGTH => "Hipertrofia & Força",
        WorkoutGoal.WEIGHT_LOSS => "Emagrecimento",
        WorkoutGoal.CONDITIONING => "Condicionamento",
        WorkoutGoal.HEALTH => "Saúde",
        _ => "Plano de Treino",
    };

    public static string Minutes(int seconds) => $"{Math.Max(1, (int)Math.Round(seconds / 60.0))}min";

    public static string Rest(int seconds) => seconds >= 60 && seconds % 60 == 0 ? $"{seconds / 60}MIN" : $"{seconds}S";

    public static string TotalTime(long seconds)
    {
        var hours = seconds / 3600;
        var minutes = seconds % 3600 / 60;
        return hours > 0 ? $"{hours}h{minutes:00}m" : $"{minutes}m";
    }

    public static string Percent(double rate) => $"{Math.Round(rate * 100)}%";

    public static string Kg(int grams) => (grams / 1000.0).ToString("0.#", PtBr);

    public static string Cover(string? url) =>
        string.IsNullOrWhiteSpace(url) ? "https://gw8hy3fdcv.ufs.sh/f/ccoBDpLoAPCO3y8pQ6GBg8iqe9pP2JrHjwd1nfKtVSQskI0v" : url;

    public static string ErrorMessage(Exception e) => e switch
    {
        ApiException { Code: ErrorCodes.AiNotConfigured } => "O Coach AI ainda não foi configurado.",
        ApiException { Code: ErrorCodes.InvalidInviteCode } => "Código de convite inválido ou expirado.",
        ApiException { Code: ErrorCodes.SessionAlreadyStarted } => "Você já iniciou este treino hoje.",
        ApiException { Code: ErrorCodes.Validation or ErrorCodes.Conflict } api => api.Message,
        ApiException => "Algo deu errado. Tente de novo.",
        HttpRequestException or TaskCanceledException => "Sem conexão com o servidor.",
        _ => "Algo deu errado. Tente de novo.",
    };
}
