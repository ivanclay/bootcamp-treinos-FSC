using FitAi.Contracts;
using Microsoft.AspNetCore.Diagnostics;

namespace FitAi.Api.Errors;

/// <summary>Traduz exceções em respostas <c>{ error, code }</c>.</summary>
public sealed class AppExceptionHandler(ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is AppException appException)
        {
            if (appException.StatusCode >= 500) logger.LogError(exception, "Request failed");
            context.Response.StatusCode = appException.StatusCode;
            await context.Response.WriteAsJsonAsync(new ErrorResponse(appException.Message, appException.Code), ct);
            return true;
        }

        logger.LogError(exception, "Unhandled exception");
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new ErrorResponse("Internal server error", ErrorCodes.Internal), ct);
        return true;
    }
}
