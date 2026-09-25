using System.Text.Json;
using FitAi.Api.Errors;
using FitAi.Api.Options;
using FitAi.Contracts;
using Microsoft.Extensions.Options;

namespace FitAi.Api.UseCases.Coach;

/// <summary>Busca vídeos de execução no YouTube. Sem chave configurada, devolve só o link de busca.</summary>
public sealed class SearchExerciseVideos(HttpClient http, IOptions<YouTubeOptions> options)
{
    private const int MaxVideos = 3;

    public sealed record Input(string ExerciseName);

    public sealed record Output(IReadOnlyList<ExerciseVideoResponse> Videos, string SearchUrl);

    public static string BuildSearchQuery(string exerciseName) => $"{exerciseName.Trim()} execução correta";

    public static string BuildSearchUrl(string exerciseName) =>
        "https://www.youtube.com/results?search_query=" + Uri.EscapeDataString(BuildSearchQuery(exerciseName));

    public async Task<Output> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var searchUrl = BuildSearchUrl(input.ExerciseName);
        var apiKey = options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey)) return new Output([], searchUrl);

        var url = "https://www.googleapis.com/youtube/v3/search?part=snippet&type=video"
            + $"&maxResults={MaxVideos}&relevanceLanguage=pt&safeSearch=strict"
            + $"&q={Uri.EscapeDataString(BuildSearchQuery(input.ExerciseName))}&key={Uri.EscapeDataString(apiKey)}";

        using var response = await http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new ExternalServiceException($"YouTube search failed with status {(int)response.StatusCode}");
        }

        try
        {
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var videos = new List<ExerciseVideoResponse>();
            foreach (var item in json.RootElement.GetProperty("items").EnumerateArray())
            {
                var videoId = item.GetProperty("id").GetProperty("videoId").GetString();
                if (string.IsNullOrEmpty(videoId)) continue;
                var snippet = item.GetProperty("snippet");
                var thumbnails = snippet.GetProperty("thumbnails");
                string? thumbnail = null;
                foreach (var size in new[] { "high", "medium", "default" })
                {
                    if (thumbnails.TryGetProperty(size, out var t)) { thumbnail = t.GetProperty("url").GetString(); break; }
                }
                videos.Add(new ExerciseVideoResponse(
                    snippet.GetProperty("title").GetString() ?? "",
                    snippet.GetProperty("channelTitle").GetString() ?? "",
                    $"https://www.youtube.com/watch?v={videoId}",
                    thumbnail ?? $"https://i.ytimg.com/vi/{videoId}/hqdefault.jpg"));
            }
            return new Output(videos, searchUrl);
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new ExternalServiceException("Unexpected YouTube search response");
        }
    }
}
