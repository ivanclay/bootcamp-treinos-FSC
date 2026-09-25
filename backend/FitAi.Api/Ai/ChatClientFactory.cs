using System.ClientModel;
using Azure.AI.OpenAI;
using FitAi.Api.Errors;
using FitAi.Api.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;

namespace FitAi.Api.Ai;

/// <summary>
/// Cria o <see cref="IChatClient"/> do provedor escolhido. Provedores do tipo "OpenAI" aceitam
/// qualquer endpoint compatível (OpenAI, Gemini, Ollama, OpenRouter, Groq...); "AzureOpenAI" usa o SDK do Azure.
/// </summary>
public sealed class ChatClientFactory(IOptions<AiOptions> options, ILoggerFactory loggerFactory)
{
    public IChatClient Create(string providerName, string? modelOverride)
    {
        if (!options.Value.Providers.TryGetValue(providerName, out var provider))
        {
            throw new AiNotConfiguredException($"AI provider '{providerName}' is not configured");
        }

        var model = string.IsNullOrWhiteSpace(modelOverride) ? provider.Model : modelOverride.Trim();
        if (string.IsNullOrWhiteSpace(model) || !provider.IsConfigured)
        {
            throw new AiNotConfiguredException($"AI provider '{providerName}' is missing its API key, endpoint or model");
        }

        IChatClient inner = provider.Type.ToLowerInvariant() switch
        {
            "azureopenai" => new AzureOpenAIClient(
                    new Uri(provider.Endpoint ?? throw new AiNotConfiguredException("Azure OpenAI requires an endpoint")),
                    new ApiKeyCredential(provider.ApiKey ?? ""))
                .GetChatClient(model)
                .AsIChatClient(),
            "openai" => new OpenAIClient(
                    new ApiKeyCredential(string.IsNullOrWhiteSpace(provider.ApiKey) ? "not-required" : provider.ApiKey),
                    new OpenAIClientOptions
                    {
                        Endpoint = string.IsNullOrWhiteSpace(provider.Endpoint) ? null : new Uri(provider.Endpoint),
                    })
                .GetChatClient(model)
                .AsIChatClient(),
            _ => throw new AiNotConfiguredException($"Unknown AI provider type '{provider.Type}'"),
        };

        return new ChatClientBuilder(inner)
            .UseFunctionInvocation(loggerFactory, c =>
            {
                c.MaximumIterationsPerRequest = options.Value.MaxToolIterations;
                // Devolve ao modelo a mensagem de erro da tool (ex.: validação do plano) para que ele corrija e tente de novo.
                c.IncludeDetailedErrors = true;
            })
            .UseLogging(loggerFactory)
            .Build();
    }
}
