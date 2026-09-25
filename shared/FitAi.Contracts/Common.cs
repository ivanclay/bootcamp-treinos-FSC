namespace FitAi.Contracts;

public sealed record ErrorResponse(string Error, string Code);

public sealed record DayConsistency(bool WorkoutDayCompleted, bool WorkoutDayStarted);

public static class ErrorCodes
{
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string UserBlocked = "USER_BLOCKED";
    public const string NotFound = "NOT_FOUND_ERROR";
    public const string Validation = "VALIDATION_ERROR";
    public const string Conflict = "CONFLICT";
    public const string WorkoutPlanNotActive = "WORKOUT_PLAN_NOT_ACTIVE";
    public const string SessionAlreadyStarted = "SESSION_ALREADY_STARTED";
    public const string InvalidInviteCode = "INVALID_INVITE_CODE";
    public const string ExternalService = "EXTERNAL_SERVICE_ERROR";
    public const string AiNotConfigured = "AI_NOT_CONFIGURED";
    public const string RateLimited = "RATE_LIMITED";
    public const string PlanLimitReached = "PLAN_LIMIT_REACHED";
    public const string PaymentProvider = "PAYMENT_PROVIDER_ERROR";
    public const string Internal = "INTERNAL_SERVER_ERROR";
}
