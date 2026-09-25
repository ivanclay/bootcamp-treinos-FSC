using System.Net;
using FitAi.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FitAi.Web.Infrastructure;

/// <summary>
/// Token vencido/usuário bloqueado: encerra a sessão e volta ao login.
/// Demais erros da API: página de erro amigável (ou JSON em chamadas AJAX).
/// </summary>
public sealed class ApiExceptionFilter(ILogger<ApiExceptionFilter> logger) : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is HttpRequestException httpError)
        {
            logger.LogError(httpError, "API unreachable");
            context.Result = ErrorResult(context, HttpStatusCode.ServiceUnavailable, "Não foi possível falar com o servidor. Tente de novo em instantes.");
            context.ExceptionHandled = true;
            return;
        }

        if (context.Exception is not ApiException api) return;

        if (api.StatusCode == HttpStatusCode.Unauthorized || api.Code == ErrorCodes.UserBlocked)
        {
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var error = api.Code == ErrorCodes.UserBlocked ? "blocked" : "expired";
            context.Result = IsAjax(context)
                ? new JsonResult(new { redirect = "/login?error=" + error }) { StatusCode = 401 }
                : new RedirectResult("/login?error=" + error);
            context.ExceptionHandled = true;
            return;
        }

        logger.LogWarning("API error {Status} {Code}: {Message}", (int)api.StatusCode, api.Code, api.Message);
        context.Result = ErrorResult(context, api.StatusCode, ErrorMessages.For(api));
        context.ExceptionHandled = true;
    }

    private static bool IsAjax(ExceptionContext context) =>
        context.HttpContext.Request.Headers.Accept.Any(a => a?.Contains("application/json") == true);

    private static IActionResult ErrorResult(ExceptionContext context, HttpStatusCode status, string message)
    {
        if (IsAjax(context)) return new JsonResult(new { error = message }) { StatusCode = (int)status };
        var result = new ViewResult { ViewName = "~/Views/Shared/Error.cshtml", StatusCode = (int)status };
        result.ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(
            new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), context.ModelState) { Model = message };
        return result;
    }
}

public static class ErrorMessages
{
    public static string For(ApiException e) => e.Code switch
    {
        ErrorCodes.NotFound => "Não encontramos o que você procurava.",
        ErrorCodes.Forbidden => "Você não tem permissão para fazer isso.",
        ErrorCodes.SessionAlreadyStarted => "Você já iniciou este treino hoje.",
        ErrorCodes.WorkoutPlanNotActive => "Este plano de treino não está mais ativo.",
        ErrorCodes.InvalidInviteCode => "Código de convite inválido ou expirado.",
        ErrorCodes.AiNotConfigured => "O Coach AI ainda não foi configurado. Avise o administrador.",
        ErrorCodes.ExternalService => "O serviço externo não respondeu. Tente de novo em instantes.",
        ErrorCodes.Validation or ErrorCodes.Conflict => e.Message,
        _ => "Algo deu errado. Tente de novo em instantes.",
    };
}
