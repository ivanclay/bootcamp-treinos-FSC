using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FitAi.Contracts;

namespace FitAi.App.Services;

public sealed class ApiException(HttpStatusCode statusCode, string message, string code) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}

/// <summary>Cliente da FitAi.Api usado pelo app. O token vem do <see cref="AuthService"/>.</summary>
public sealed class ApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http = new() { BaseAddress = new Uri(AppConfig.ApiBaseUrl.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(120) };

    public string? AccessToken { get; set; }

    /// <summary>Disparado quando a API responde 401 ou usuário bloqueado.</summary>
    public event EventHandler? SessionExpired;

    public static string GoogleLoginUrl(string codeChallenge) =>
        $"{AppConfig.ApiBaseUrl}/auth/google/login?redirectUri={Uri.EscapeDataString(AppConfig.CallbackUri)}&codeChallenge={codeChallenge}&codeChallengeMethod=S256";

    public Task<AuthProvidersResponse> GetAuthProvidersAsync() => Send<AuthProvidersResponse>(HttpMethod.Get, "auth/providers");
    public Task<AuthTokenResponse> ExchangeCodeAsync(string code, string codeVerifier) => Send<AuthTokenResponse>(HttpMethod.Post, "auth/token", new ExchangeAuthCodeRequest(code, codeVerifier));
    public Task<AuthTokenResponse> DevLoginAsync(string email) => Send<AuthTokenResponse>(HttpMethod.Post, "auth/dev-login", new DevLoginRequest(email, null));
    public Task<CurrentUserResponse> GetCurrentUserAsync() => Send<CurrentUserResponse>(HttpMethod.Get, "auth/me");

    public Task<HomeDataResponse> GetHomeAsync(DateOnly date) => Send<HomeDataResponse>(HttpMethod.Get, $"home/{date:yyyy-MM-dd}");
    public Task<UserTrainDataResponse?> GetTrainDataAsync() => Send<UserTrainDataResponse?>(HttpMethod.Get, "me");
    public Task<UserTrainDataResponse> UpsertTrainDataAsync(UpsertUserTrainDataRequest body) => Send<UserTrainDataResponse>(HttpMethod.Put, "me", body);
    public Task<TeacherLinkResponse> RedeemInviteCodeAsync(string code) => Send<TeacherLinkResponse>(HttpMethod.Post, "me/teacher", new RedeemInviteCodeRequest { Code = code });
    public Task<IReadOnlyList<PendingInviteResponse>> ListPendingInvitesAsync() => Send<IReadOnlyList<PendingInviteResponse>>(HttpMethod.Get, "me/invites");
    public Task<TeacherLinkResponse> AcceptInviteAsync(Guid id) => Send<TeacherLinkResponse>(HttpMethod.Post, $"me/invites/{id}/accept");
    public Task<object?> DeclineInviteAsync(Guid id) => Send<object?>(HttpMethod.Post, $"me/invites/{id}/decline");
    public Task<MyPlanResponse> GetMyPlanAsync() => Send<MyPlanResponse>(HttpMethod.Get, "me/plan");
    public Task<StatsResponse> GetStatsAsync(DateOnly from, DateOnly to) => Send<StatsResponse>(HttpMethod.Get, $"stats?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
    public Task<IReadOnlyList<WorkoutPlanResponse>> ListActivePlansAsync() => Send<IReadOnlyList<WorkoutPlanResponse>>(HttpMethod.Get, "workout-plans?active=true");
    public Task<WorkoutDayResponse> GetWorkoutDayAsync(Guid planId, Guid dayId) => Send<WorkoutDayResponse>(HttpMethod.Get, $"workout-plans/{planId}/days/{dayId}");
    public Task<StartWorkoutSessionResponse> StartSessionAsync(Guid planId, Guid dayId) => Send<StartWorkoutSessionResponse>(HttpMethod.Post, $"workout-plans/{planId}/days/{dayId}/sessions");
    public Task<WorkoutSessionResponse> CompleteSessionAsync(Guid planId, Guid dayId, Guid sessionId) =>
        Send<WorkoutSessionResponse>(HttpMethod.Patch, $"workout-plans/{planId}/days/{dayId}/sessions/{sessionId}", new CompleteWorkoutSessionRequest { CompletedAt = DateTimeOffset.UtcNow });
    public Task<CoachChatResponse> SendCoachMessageAsync(CoachChatRequest body) => Send<CoachChatResponse>(HttpMethod.Post, "coach/chat", body);

    private async Task<T> Send<T>(HttpMethod method, string path, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (!string.IsNullOrEmpty(AccessToken)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        if (body is not null) request.Content = JsonContent.Create(body, body.GetType(), options: Json);

        using var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            ErrorResponse? error = null;
            try { error = await response.Content.ReadFromJsonAsync<ErrorResponse>(Json); }
            catch (Exception e) when (e is JsonException or NotSupportedException) { }

            var code = error?.Code ?? "HTTP_" + (int)response.StatusCode;
            if (response.StatusCode == HttpStatusCode.Unauthorized || code == ErrorCodes.UserBlocked)
            {
                SessionExpired?.Invoke(this, EventArgs.Empty);
            }
            throw new ApiException(response.StatusCode, error?.Error ?? "Erro ao falar com o servidor", code);
        }

        if (response.StatusCode == HttpStatusCode.NoContent) return default!;
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }
}
