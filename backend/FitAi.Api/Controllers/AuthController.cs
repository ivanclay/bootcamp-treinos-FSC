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
using Microsoft.AspNetCore.RateLimiting;
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

    /// <summary>
    /// Inicia o login com o Google. Ao final, redireciona para <c>redirectUri?code=...</c>.
    /// <c>codeChallenge</c> é obrigatório (PKCE S256): o código só é trocado por token com o verifier correspondente.
    /// </summary>
    [HttpGet("google/login")]
    [EnableRateLimiting(RateLimiting.Auth)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public IActionResult GoogleLogin([FromQuery] string redirectUri, [FromQuery] string? codeChallenge, [FromQuery] string? codeChallengeMethod = "S256")
    {
        EnsureAllowedRedirect(redirectUri);
        if (codeChallengeMethod != "S256" || !Pkce.IsValidChallenge(codeChallenge))
        {
            throw new ValidationException("codeChallenge (PKCE S256) é obrigatório");
        }
        if (!authOptions.Value.Google.IsConfigured) throw new ValidationException("O login com Google não está configurado");

        var callback = Url.Action(nameof(GoogleCallback), new { redirectUri, codeChallenge })!;
        return Challenge(new AuthenticationProperties { RedirectUri = callback }, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("google/callback")]
    [EnableRateLimiting(RateLimiting.Auth)]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> GoogleCallback(
        [FromQuery] string redirectUri,
        [FromQuery] string codeChallenge,
        [FromServices] SignInWithExternalLogin signIn,
        [FromServices] CreateAuthCode createAuthCode,
        CancellationToken ct)
    {
        EnsureAllowedRedirect(redirectUri);
        if (!Pkce.IsValidChallenge(codeChallenge)) return Redirect(AppendQuery(redirectUri, "error", "login_failed"));
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
            var code = await createAuthCode.ExecuteAsync(new CreateAuthCode.Input(user.UserId, codeChallenge), ct);
            return Redirect(AppendQuery(redirectUri, "code", code.Code));
        }
        catch (UserBlockedException)
        {
            return Redirect(AppendQuery(redirectUri, "error", "blocked"));
        }
    }

    /// <summary>Troca o código de uso único recebido no redirect por um JWT.</summary>
    [HttpPost("token")]
    [EnableRateLimiting(RateLimiting.Auth)]
    [ProducesResponseType<AuthTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public Task<AuthTokenResponse> ExchangeCode(
        ExchangeAuthCodeRequest request, [FromServices] ExchangeAuthCode exchangeAuthCode, CancellationToken ct) =>
        exchangeAuthCode.ExecuteAsync(new ExchangeAuthCode.Input(request.Code, request.CodeVerifier), ct);

    /// <summary>Login sem Google, só em desenvolvimento (<c>Auth:EnableDevLogin</c>).</summary>
    [HttpPost("dev-login")]
    [EnableRateLimiting(RateLimiting.Auth)]
    [ProducesResponseType<AuthTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthTokenResponse>> DevLogin(
        DevLoginRequest request,
        [FromServices] SignInWithExternalLogin signIn,
        [FromServices] IssueAuthToken issueAuthToken,
        CancellationToken ct)
    {
        if (!DevLoginEnabled) throw new NotFoundException("Not found");
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
        if (!allowed) throw new ValidationException("redirectUri não permitido");
    }

    private static string AppendQuery(string uri, string key, string value) =>
        uri + (uri.Contains('?') ? "&" : "?") + key + "=" + Uri.EscapeDataString(value);
}
