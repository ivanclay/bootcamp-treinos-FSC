using FitAi.Contracts;

namespace FitAi.Api.Errors;

/// <summary>
/// Erro de negócio lançado pelos use cases. O <see cref="AppExceptionHandler"/> converte
/// em <see cref="ErrorResponse"/> com o status HTTP correspondente.
/// </summary>
public abstract class AppException(string message, int statusCode, string code) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}

public sealed class NotFoundException(string message)
    : AppException(message, StatusCodes.Status404NotFound, ErrorCodes.NotFound);

public sealed class UnauthorizedException(string message = "Unauthorized")
    : AppException(message, StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized);

public sealed class ForbiddenException(string message = "Forbidden")
    : AppException(message, StatusCodes.Status403Forbidden, ErrorCodes.Forbidden);

public sealed class UserBlockedException()
    : AppException("User is blocked", StatusCodes.Status403Forbidden, ErrorCodes.UserBlocked);

public sealed class ValidationException(string message)
    : AppException(message, StatusCodes.Status400BadRequest, ErrorCodes.Validation);

public sealed class ConflictException(string message)
    : AppException(message, StatusCodes.Status409Conflict, ErrorCodes.Conflict);

public sealed class WorkoutPlanNotActiveException(string message)
    : AppException(message, StatusCodes.Status422UnprocessableEntity, ErrorCodes.WorkoutPlanNotActive);

public sealed class SessionAlreadyStartedException(string message)
    : AppException(message, StatusCodes.Status409Conflict, ErrorCodes.SessionAlreadyStarted);

public sealed class InvalidInviteCodeException(string message)
    : AppException(message, StatusCodes.Status400BadRequest, ErrorCodes.InvalidInviteCode);

public sealed class ExternalServiceException(string message)
    : AppException(message, StatusCodes.Status502BadGateway, ErrorCodes.ExternalService);

public sealed class AiNotConfiguredException(string message)
    : AppException(message, StatusCodes.Status503ServiceUnavailable, ErrorCodes.AiNotConfigured);

/// <summary>Limite do plano gratuito atingido (a mensagem explica o limite e como liberar).</summary>
public sealed class PlanLimitException(string message)
    : AppException(message, StatusCodes.Status403Forbidden, ErrorCodes.PlanLimitReached);

public sealed class PaymentProviderException(string message)
    : AppException(message, StatusCodes.Status502BadGateway, ErrorCodes.PaymentProvider);
