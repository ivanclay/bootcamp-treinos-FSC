using FitAi.Api.Ai;
using FitAi.Api.Options;
using FitAi.Contracts;
using Microsoft.Extensions.Options;

namespace FitAi.Api.UseCases.Admin;

public sealed class GetAiSettings(AiSettingsStore store, IOptions<AiOptions> options)
{
    public async Task<AiSettingsResponse> ExecuteAsync(CancellationToken ct = default)
    {
        var settings = await store.GetAsync(ct);
        var providers = options.Value.Providers
            .Select(p => new AiProviderResponse(p.Key, p.Value.Type, p.Value.Model, p.Value.IsConfigured))
            .OrderBy(p => p.Name)
            .ToList();
        var defaultModel = options.Value.Providers.TryGetValue(settings.Provider, out var provider) ? provider.Model : "";
        return new AiSettingsResponse(
            settings.Provider,
            string.IsNullOrWhiteSpace(settings.Model) ? defaultModel : settings.Model,
            string.IsNullOrWhiteSpace(settings.SystemPrompt) ? CoachPrompt.Default : settings.SystemPrompt,
            !string.IsNullOrWhiteSpace(settings.SystemPrompt),
            settings.UpperBodyCoverImages,
            settings.LowerBodyCoverImages,
            providers);
    }
}
