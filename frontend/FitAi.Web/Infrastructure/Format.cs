using System.Globalization;
using FitAi.Contracts;

namespace FitAi.Web.Infrastructure;

/// <summary>Formatação em português para as telas.</summary>
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

    public static string DayInitial(WeekDay day) => DayName(day)[..1];

    public static string Goal(WorkoutGoal? goal) => goal switch
    {
        WorkoutGoal.HYPERTROPHY => "Hipertrofia",
        WorkoutGoal.STRENGTH => "Força",
        WorkoutGoal.HYPERTROPHY_AND_STRENGTH => "Hipertrofia & Força",
        WorkoutGoal.WEIGHT_LOSS => "Emagrecimento",
        WorkoutGoal.CONDITIONING => "Condicionamento",
        WorkoutGoal.HEALTH => "Saúde",
        _ => "Sem objetivo",
    };

    public static string Role(UserRole role) => role switch
    {
        UserRole.ADMIN => "Admin",
        UserRole.TEACHER => "Professor",
        _ => "Aluno",
    };

    public static string Money(decimal value) => value.ToString("C", PtBr);

    public static string Plan(PlanType plan) => plan == PlanType.PRO ? "Plano Pro" : "Plano Básico";

    public static string Cycle(BillingCycle cycle) => cycle == BillingCycle.YEARLY ? "Anual" : "Mensal";

    public static string Method(PaymentMethod method) => method switch
    {
        PaymentMethod.PIX => "PIX",
        PaymentMethod.BOLETO => "Boleto",
        _ => "Cartão de crédito",
    };

    public static string SubscriptionStatus(SubscriptionStatus status) => status switch
    {
        FitAi.Contracts.SubscriptionStatus.ACTIVE => "Ativa",
        FitAi.Contracts.SubscriptionStatus.PAST_DUE => "Pagamento atrasado",
        FitAi.Contracts.SubscriptionStatus.CANCELED => "Cancelada",
        _ => "Aguardando pagamento",
    };

    public static string PaymentStatus(PaymentStatus status) => status switch
    {
        FitAi.Contracts.PaymentStatus.CONFIRMED or FitAi.Contracts.PaymentStatus.RECEIVED => "Pago",
        FitAi.Contracts.PaymentStatus.OVERDUE => "Vencido",
        FitAi.Contracts.PaymentStatus.REFUNDED => "Estornado",
        FitAi.Contracts.PaymentStatus.CANCELED => "Cancelado",
        _ => "Em aberto",
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

    public static string Date(DateTimeOffset? value, TimeZoneInfo tz) =>
        value is null ? "—" : TimeZoneInfo.ConvertTime(value.Value, tz).ToString("dd/MM/yyyy HH:mm", PtBr);

    public static string ShortDate(DateTimeOffset? value, TimeZoneInfo tz) =>
        value is null ? "—" : TimeZoneInfo.ConvertTime(value.Value, tz).ToString("dd/MM", PtBr);

    public static string MonthShort(int month) => PtBr.DateTimeFormat.GetAbbreviatedMonthName(month).TrimEnd('.') is var m && m.Length > 0
        ? char.ToUpper(m[0]) + m[1..]
        : "";
}
