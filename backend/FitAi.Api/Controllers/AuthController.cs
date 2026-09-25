using System.Security.Claims;
using FitAi.Api.Auth;
using FitAi.Api.Errors;
using FitAi.Api.Options;
using FitAi.Api.UseCases.Auth;
using FitAi.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FitAi.Api.Controllers;

[ApiController]
[Route("auth")]
[Tags("Auth")]
public sealed class AuthController(IOptions<AuthOptions> authOptions, IWebHostEnvironment environment) : ControllerBase
{
    public const string ExternalScheme = "External";

    private bool DevLoginEnabled => authOptions.Value.EnableDevLogin && environment.IsDevelopment();

    /// <summary>Quais formas de login estão disponíveis.</summary>
    [HttpGet("providers")]
    public AuthProvidersResponse GetProviders() => new(authOptions.Value.Google.IsConfigured, DevLoginEnabled);

    /// <summary>Inicia o login com o Google. Ao final, redireciona para <c>redirectUri?code=...</c>.</summary>
    [HttpGet("google/login")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public IActionResult GoogleLogin([FromQuery] string redirectUri)
    {
        EnsureAllowedRedirect(redirectUri);
        if (!authOptions.Value.Google.IsConfigured) throw new ValidationException("Google login is not configured");

        var callback = Url.Action(nameof(GoogleCallback), new { redirectUri })!;
        return Challenge(new AuthenticationProperties { RedirectUri = callback }, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("google/callback")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> GoogleCallback(
        [FromQuery] string redirectUri,
        [FromServices] SignInWithExternalLogin signIn,
        [FromServices] CreateAuthCode createAuthCode,
        CancellationToken ct)
    {
        EnsureAllowedRedirect(redirectUri);
        var result = await HttpContext.AuthenticateAsync(ExternalScheme);
        await HttpContext.SignOutAsync(ExternalScheme);
        if (!result.Succeeded) return Redirect(AppendQuery(redirectUri, "error", "login_failed"));

        var principal = result.Principal;
        var providerKey = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal.FindFirstValue(ClaimTypes.Email);
        if (providerKey is null || email is null) return Redirect(AppendQuery(redirectUri, "error", "login_failed"));

        try
        {
            var user = await signIn.ExecuteAsync(new SignInWithExternalLogin.Input(
                "google",
                providerKey,
                email,
                principal.FindFirstValue(ClaimTypes.Name),
                principal.FindFirstValue("picture"),
                EmailVerified: true), ct);
            var code = await createAuthCode.ExecuteAsync(new CreateAuthCode.Input(user.UserId), ct);
            return Redirect(AppendQuery(redirectUri, "code", code.Code));
        }
        catch (UserBlockedException)
        {
            return Redirect(AppendQuery(redirectUri, "error", "blocked"));
        }
    }

    /// <summary>Troca o código de uso único recebido no redirect por um JWT.</summary>
    [HttpPost("token")]
    [ProducesResponseType<AuthTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public Task<AuthTokenResponse> ExchangeCode(
        ExchangeAuthCodeRequest request, [FromServices] ExchangeAuthCode exchangeAuthCode, CancellationToken ct) =>
        exchangeAuthCode.ExecuteAsync(new ExchangeAuthCode.Input(request.Code), ct);

    /// <summary>Login sem Google, só em desenvolvimento (<c>Auth:EnableDevLogin</c>).</summary>
    [HttpPost("dev-login")]
    [ProducesResponseType<AuthTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthTokenResponse>> DevLogin(
        DevLoginRequest request,
        [FromServices] SignInWithExternalLogin signIn,
        [FromServices] IssueAuthToken issueAuthToken,
        CancellationToken ct)
    {
        if (!DevLoginEnabled) return NotFound();
        var user = await signIn.ExecuteAsync(new SignInWithExternalLogin.Input(
            "dev", request.Email.Trim().ToLowerInvariant(), request.Email, request.Name, null, EmailVerified: false), ct);
        return await issueAuthToken.ExecuteAsync(new IssueAuthToken.Input(user.UserId), ct);
    }

    /// <summary>Usuário autenticado, com papel e professor.</summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<CurrentUserResponse> Me(
        [FromServices] ICurrentUser currentUser, [FromServices] GetCurrentUser getCurrentUser, CancellationToken ct) =>
        await getCurrentUser.ExecuteAsync(new GetCurrentUser.Input(await currentUser.GetAsync(ct)), ct);

    private void EnsureAllowedRedirect(string redirectUri)
    {
        var allowed = !string.IsNullOrWhiteSpace(redirectUri) && authOptions.Value.AllowedRedirectUris.Any(prefix =>
            redirectUri.Equals(prefix, StringComparison.OrdinalIgnoreCase)
            || redirectUri.StartsWith(prefix.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)
            || redirectUri.StartsWith(prefix + "?", StringComparison.OrdinalIgnoreCase));
        if (!allowed) throw new ValidationException("redirectUri is not allowed");
    }

    private static string AppendQuery(string uri, string key, string value) =>
        uri + (uri.Contains('?') ? "&" : "?") + key + "=" + Uri.EscapeDataString(value);
}
