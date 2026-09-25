namespace FitAi.Contracts;

public sealed record ExchangeAuthCodeRequest(string Code);

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
