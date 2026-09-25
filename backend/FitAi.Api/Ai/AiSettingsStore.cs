using System.Text.Json;
using FitAi.Api.Data;
using FitAi.Api.Entities;
using FitAi.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FitAi.Api.Ai;

/// <summary>Configurações do Coach AI editáveis no painel; o que não foi salvo vem do appsettings.</summary>
public sealed record AiSettings(
    string Provider,
    string? Model,
    string? SystemPrompt,
    IReadOnlyList<string> UpperBodyCoverImages,
    IReadOnlyList<string> LowerBodyCoverImages);

public sealed class AiSettingsStore(AppDbContext db, IOptions<AiOptions> options)
{
    private const string Key = "ai";

    public async Task<AiSettings> GetAsync(CancellationToken ct)
    {
        var row = await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == Key, ct);
        var saved = row is null ? null : JsonSerializer.Deserialize<AiSettings>(row.Value);
        return new AiSettings(
            saved?.Provider is { Length: > 0 } provider && options.Value.Providers.ContainsKey(provider)
                ? provider
                : options.Value.DefaultProvider,
            saved?.Model,
            saved?.SystemPrompt,
            saved?.UpperBodyCoverImages is { Count: > 0 } upper ? upper : CoachPrompt.DefaultUpperBodyCoverImages,
            saved?.LowerBodyCoverImages is { Count: > 0 } lower ? lower : CoachPrompt.DefaultLowerBodyCoverImages);
    }

    public async Task SaveAsync(AiSettings settings, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(settings);
        var row = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == Key, ct);
        if (row is null) db.AppSettings.Add(new AppSetting { Key = Key, Value = json });
        else row.Value = json;
        await db.SaveChangesAsync(ct);
    }
}
