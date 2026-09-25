using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FitAi.Contracts;

namespace FitAi.Web.Infrastructure;

/// <summary>Erro devolvido pela API (<c>{ error, code }</c>).</summary>
public sealed class ApiException(HttpStatusCode statusCode, string message, string code) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}

/// <summary>Cliente tipado da FitAi.Api. O token do usuário logado é anexado pelo <see cref="BearerTokenHandler"/>.</summary>
public sealed class ApiClient(HttpClient http, IConfiguration configuration)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string BaseUrl => http.BaseAddress!.ToString().TrimEnd('/');

    // ---------- Auth ----------
    public Task<AuthProvidersResponse> GetAuthProvidersAsync(CancellationToken ct) => Get<AuthProvidersResponse>("auth/providers", ct);
    public Task<AuthTokenResponse> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken ct) => Send<AuthTokenResponse>(HttpMethod.Post, "auth/token", new ExchangeAuthCodeRequest(code, codeVerifier), ct);
    public Task<AuthTokenResponse> DevLoginAsync(string email, string? name, CancellationToken ct) => Send<AuthTokenResponse>(HttpMethod.Post, "auth/dev-login", new DevLoginRequest(email, name), ct);
    public Task<CurrentUserResponse> GetCurrentUserAsync(CancellationToken ct) => Get<CurrentUserResponse>("auth/me", ct);
    /// <summary>URL da API vista pelo navegador (Api:PublicUrl); difere de Api:BaseUrl quando a Web fala com a API por rede interna.</summary>
    public string PublicUrl => (configuration["Api:PublicUrl"] ?? BaseUrl).TrimEnd('/');

    public string GoogleLoginUrl(string redirectUri, string codeChallenge) =>
        $"{PublicUrl}/auth/google/login?redirectUri={Uri.EscapeDataString(redirectUri)}&codeChallenge={codeChallenge}&codeChallengeMethod=S256";

    // ---------- Aluno ----------
    public Task<HomeDataResponse> GetHomeAsync(DateOnly date, CancellationToken ct) => Get<HomeDataResponse>($"home/{date:yyyy-MM-dd}", ct);
    public Task<UserTrainDataResponse?> GetTrainDataAsync(CancellationToken ct) => Get<UserTrainDataResponse?>("me", ct);
    public Task<UserTrainDataResponse> UpsertTrainDataAsync(UpsertUserTrainDataRequest body, CancellationToken ct) => Send<UserTrainDataResponse>(HttpMethod.Put, "me", body, ct);
    public Task<TeacherLinkResponse> RedeemInviteCodeAsync(string code, CancellationToken ct) => Send<TeacherLinkResponse>(HttpMethod.Post, "me/teacher", new RedeemInviteCodeRequest { Code = code }, ct);
    public Task<IReadOnlyList<PendingInviteResponse>> ListPendingInvitesAsync(CancellationToken ct) => Get<IReadOnlyList<PendingInviteResponse>>("me/invites", ct);
    public Task<TeacherLinkResponse> AcceptInviteAsync(Guid id, CancellationToken ct) => Send<TeacherLinkResponse>(HttpMethod.Post, $"me/invites/{id}/accept", null, ct);
    public Task DeclineInviteAsync(Guid id, CancellationToken ct) => Send<object?>(HttpMethod.Post, $"me/invites/{id}/decline", null, ct);
    public Task<StatsResponse> GetStatsAsync(DateOnly from, DateOnly to, CancellationToken ct) => Get<StatsResponse>($"stats?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", ct);
    public Task<IReadOnlyList<WorkoutPlanResponse>> ListWorkoutPlansAsync(bool? active, CancellationToken ct) => Get<IReadOnlyList<WorkoutPlanResponse>>(active is null ? "workout-plans" : $"workout-plans?active={active.Value.ToString().ToLowerInvariant()}", ct);
    public Task<WorkoutPlanSummaryResponse> GetWorkoutPlanAsync(Guid id, CancellationToken ct) => Get<WorkoutPlanSummaryResponse>($"workout-plans/{id}", ct);
    public Task<WorkoutDayResponse> GetWorkoutDayAsync(Guid planId, Guid dayId, CancellationToken ct) => Get<WorkoutDayResponse>($"workout-plans/{planId}/days/{dayId}", ct);
    public Task<StartWorkoutSessionResponse> StartSessionAsync(Guid planId, Guid dayId, CancellationToken ct) => Send<StartWorkoutSessionResponse>(HttpMethod.Post, $"workout-plans/{planId}/days/{dayId}/sessions", null, ct);
    public Task<WorkoutSessionResponse> CompleteSessionAsync(Guid planId, Guid dayId, Guid sessionId, CancellationToken ct) => Send<WorkoutSessionResponse>(HttpMethod.Patch, $"workout-plans/{planId}/days/{dayId}/sessions/{sessionId}", new CompleteWorkoutSessionRequest { CompletedAt = DateTimeOffset.UtcNow }, ct);
    public Task<CoachChatResponse> SendCoachMessageAsync(CoachChatRequest body, CancellationToken ct) => Send<CoachChatResponse>(HttpMethod.Post, "coach/chat", body, ct);

    public Task<MyPlanResponse> GetMyPlanAsync(CancellationToken ct) => Get<MyPlanResponse>("me/plan", ct);

    // ---------- Assinatura (professor) ----------
    public Task<BillingStatusResponse> GetBillingAsync(CancellationToken ct) => Get<BillingStatusResponse>("admin/billing", ct);
    public Task<CheckoutResponse> CheckoutAsync(CheckoutRequest body, CancellationToken ct) => Send<CheckoutResponse>(HttpMethod.Post, "admin/billing/checkout", body, ct);
    public Task<CheckoutResponse> GetPendingPaymentAsync(CancellationToken ct) => Get<CheckoutResponse>("admin/billing/pending", ct);
    public Task<SubscriptionResponse> CancelSubscriptionAsync(CancellationToken ct) => Send<SubscriptionResponse>(HttpMethod.Post, "admin/billing/cancel", null, ct);
    public Task SimulatePaymentAsync(CancellationToken ct) => Send<object?>(HttpMethod.Post, "admin/billing/simulate-payment", null, ct);

    // ---------- Admin ----------
    public Task<AdminDashboardResponse> GetDashboardAsync(CancellationToken ct) => Get<AdminDashboardResponse>("admin/dashboard", ct);

    public Task<PagedResponse<AdminUserListItemResponse>> ListUsersAsync(string? search, UserRole? role, string? teacherId, int page, int pageSize, CancellationToken ct)
    {
        var query = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(search)) query.Add("search=" + Uri.EscapeDataString(search));
        if (role is not null) query.Add("role=" + role);
        if (!string.IsNullOrWhiteSpace(teacherId)) query.Add("teacherId=" + Uri.EscapeDataString(teacherId));
        return Get<PagedResponse<AdminUserListItemResponse>>("admin/users?" + string.Join('&', query), ct);
    }

    public Task<AdminUserDetailResponse> GetUserAsync(string id, CancellationToken ct) => Get<AdminUserDetailResponse>($"admin/users/{Uri.EscapeDataString(id)}", ct);
    public Task<AdminUserListItemResponse> UpdateUserAsync(string id, UpdateUserRequest body, CancellationToken ct) => Send<AdminUserListItemResponse>(HttpMethod.Patch, $"admin/users/{Uri.EscapeDataString(id)}", body, ct);
    public Task<WorkoutPlanResponse> CreateStudentPlanAsync(string userId, SaveWorkoutPlanRequest body, CancellationToken ct) => Send<WorkoutPlanResponse>(HttpMethod.Post, $"admin/users/{Uri.EscapeDataString(userId)}/workout-plans", body, ct);
    public Task<WorkoutPlanResponse> UpdateStudentPlanAsync(Guid planId, SaveWorkoutPlanRequest body, CancellationToken ct) => Send<WorkoutPlanResponse>(HttpMethod.Put, $"admin/workout-plans/{planId}", body, ct);
    public Task<WorkoutPlanResponse> ActivateStudentPlanAsync(Guid planId, CancellationToken ct) => Send<WorkoutPlanResponse>(HttpMethod.Post, $"admin/workout-plans/{planId}/activate", null, ct);
    public Task DeleteStudentPlanAsync(Guid planId, CancellationToken ct) => Send<object?>(HttpMethod.Delete, $"admin/workout-plans/{planId}", null, ct);
    public Task<IReadOnlyList<EmailInviteResponse>> ListInvitesAsync(CancellationToken ct) => Get<IReadOnlyList<EmailInviteResponse>>("admin/invites", ct);
    public Task<EmailInviteResponse> CreateInviteAsync(CreateEmailInviteRequest body, CancellationToken ct) => Send<EmailInviteResponse>(HttpMethod.Post, "admin/invites", body, ct);
    public Task DeleteInviteAsync(Guid id, CancellationToken ct) => Send<object?>(HttpMethod.Delete, $"admin/invites/{id}", null, ct);
    public Task<IReadOnlyList<InviteCodeResponse>> ListInviteCodesAsync(CancellationToken ct) => Get<IReadOnlyList<InviteCodeResponse>>("admin/invite-codes", ct);
    public Task<InviteCodeResponse> CreateInviteCodeAsync(CreateInviteCodeRequest body, CancellationToken ct) => Send<InviteCodeResponse>(HttpMethod.Post, "admin/invite-codes", body, ct);
    public Task<InviteCodeResponse> DeactivateInviteCodeAsync(Guid id, CancellationToken ct) => Send<InviteCodeResponse>(HttpMethod.Post, $"admin/invite-codes/{id}/deactivate", null, ct);
    public Task<AiSettingsResponse> GetAiSettingsAsync(CancellationToken ct) => Get<AiSettingsResponse>("admin/ai-settings", ct);
    public Task<AiSettingsResponse> UpdateAiSettingsAsync(UpdateAiSettingsRequest body, CancellationToken ct) => Send<AiSettingsResponse>(HttpMethod.Put, "admin/ai-settings", body, ct);

    // ---------- HTTP ----------
    private Task<T> Get<T>(string path, CancellationToken ct) => Send<T>(HttpMethod.Get, path, null, ct);

    private async Task<T> Send<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        using var response = await http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            ErrorResponse? error = null;
            try { error = await response.Content.ReadFromJsonAsync<ErrorResponse>(Json, ct); }
            catch (JsonException) { }
            catch (NotSupportedException) { }
            throw new ApiException(response.StatusCode, error?.Error ?? response.ReasonPhrase ?? "Erro", error?.Code ?? "HTTP_" + (int)response.StatusCode);
        }

        if (response.StatusCode == HttpStatusCode.NoContent || response.Content.Headers.ContentLength == 0) return default!;
        return (await response.Content.ReadFromJsonAsync<T>(Json, ct))!;
    }
}

/// <summary>Anexa o JWT guardado no cookie de login a cada chamada da API.</summary>
public sealed class BearerTokenHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = accessor.HttpContext?.User.FindFirst(UserClaims.AccessToken)?.Value;
        if (!string.IsNullOrEmpty(token)) request.Headers.Authorization = new("Bearer", token);
        return base.SendAsync(request, ct);
    }
}
