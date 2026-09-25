namespace FitAi.Web.Infrastructure;

/// <summary>"Hoje" no fuso da aplicação (App:TimeZone, padrão America/Sao_Paulo).</summary>
public sealed class AppClock(IConfiguration configuration, TimeProvider timeProvider)
{
    public TimeZoneInfo TimeZone { get; } = FindTimeZone(configuration["App:TimeZone"] ?? "America/Sao_Paulo");

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), TimeZone).DateTime);

    private static TimeZoneInfo FindTimeZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
    }
}
