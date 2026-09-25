namespace FitAi.Contracts;

/// <summary>Troca do código de login por um JWT. <c>CodeVerifier</c> é o segredo PKCE criado pelo cliente.</summary>
public sealed record ExchangeAuthCodeRequest(string Code, string CodeVerifier);

public sealed record DevLoginRequest(string Email, string? Name);

public sealed record AuthTokenResponse(string AccessToken, DateTimeOffset ExpiresAt, CurrentUserResponse User);

public sealed record CurrentUserResponse(
    string Id,
    string Name,
    string Email,
    string? Image,
    UserRole Role,
    string? TeacherId,
    string? TeacherName,
    bool AllowAiWorkoutPlans,
    bool HasTrainData,
    bool HasActiveWorkoutPlan);

public sealed record AuthProvidersResponse(bool Google, bool DevLogin);
