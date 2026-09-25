using FitAi.Api.Ai;
using FitAi.Api.Errors;
using FitAi.Api.Options;
using FitAi.Contracts;
using Microsoft.Extensions.Options;

namespace FitAi.Api.UseCases.Admin;

/// <summary>Salva provedor, modelo, prompt e imagens de capa. Chaves de API ficam só na configuração.</summary>
public sealed class UpdateAiSettings(AiSettingsStore store, IOptions<AiOptions> options, GetAiSettings getAiSettings)
{
    public sealed record Input(UpdateAiSettingsRequest Settings);

    public async Task<AiSettingsResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var request = input.Settings;
        if (!options.Value.Providers.TryGetValue(request.Provider, out var provider))
        {
            throw new ValidationException($"Provedor de IA desconhecido: '{request.Provider}'");
        }

        var upper = CleanUrls(request.UpperBodyCoverImages);
        var lower = CleanUrls(request.LowerBodyCoverImages);
        var model = string.IsNullOrWhiteSpace(request.Model) || request.Model.Trim() == provider.Model ? null : request.Model.Trim();
        var prompt = string.IsNullOrWhiteSpace(request.SystemPrompt) || request.SystemPrompt.Trim() == CoachPrompt.Default.Trim()
            ? null
            : request.SystemPrompt.Trim();

        var providerName = options.Value.Providers.Keys.First(k => string.Equals(k, request.Provider, StringComparison.OrdinalIgnoreCase));
        await store.SaveAsync(new AiSettings(providerName, model, prompt, upper, lower), ct);
        return await getAiSettings.ExecuteAsync(ct);
    }

    private static List<string> CleanUrls(IEnumerable<string> urls)
    {
        var result = urls.Select(u => u.Trim()).Where(u => u.Length > 0).Distinct().ToList();
        var invalid = result.FirstOrDefault(u => !Uri.TryCreate(u, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"));
        if (invalid is not null) throw new ValidationException($"URL de imagem inválida: {invalid}");
        return result;
    }
}
