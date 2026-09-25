using FitAi.Contracts;
using FitAi.Web.Infrastructure;
using FitAi.Web.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;

namespace FitAi.Web.Controllers;

[AllowAnonymous]
public sealed class AccountController(ApiClient api, IMemoryCache cache) : Controller
{
    [HttpGet("/login")]
    public async Task<IActionResult> Login(string? error, CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated == true) return Redirect("/");
        AuthProvidersResponse providers;
        // Cache curto: a tela de login é pública e não deve gerar uma chamada à API por visita.
        try
        {
            providers = (await cache.GetOrCreateAsync("auth-providers", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                return api.GetAuthProvidersAsync(ct);
            }))!;
        }
        catch (HttpRequestException) { providers = new AuthProvidersResponse(false, false); error ??= "offline"; }
        return View(new LoginViewModel(providers, error));
    }

    private const string PkceCookie = "fitai.pkce";

    /// <summary>
    /// Envia para o login do Google (feito pela API), que volta em /auth/callback com um código.
    /// O verifier PKCE fica num cookie deste navegador: um código gerado em outro navegador (login CSRF) não é aceito.
    /// </summary>
    [HttpGet("/login/google")]
    [EnableRateLimiting("login")]
    public IActionResult Google()
    {
        var verifier = Pkce.CreateVerifier();
        Response.Cookies.Append(PkceCookie, verifier, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/auth/callback",
            MaxAge = TimeSpan.FromMinutes(10),
        });
        return Redirect(api.GoogleLoginUrl(CallbackUrl(), Pkce.CreateChallenge(verifier)));
    }

    [HttpGet("/auth/callback")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Callback(string? code, string? error, CancellationToken ct)
    {
        var verifier = Request.Cookies[PkceCookie];
        Response.Cookies.Delete(PkceCookie, new CookieOptions { Path = "/auth/callback" });
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code)) return Redirect("/login?error=" + (error ?? "login_failed"));
        if (string.IsNullOrEmpty(verifier)) return Redirect("/login?error=login_failed");
        try
        {
            var auth = await api.ExchangeCodeAsync(code, verifier, ct);
            await UserClaims.SignInAsync(HttpContext, auth);
            return Redirect(HomeFor(auth.User));
        }
        catch (ApiException e)
        {
            return Redirect("/login?error=" + (e.Code == ErrorCodes.UserBlocked ? "blocked" : "login_failed"));
        }
    }

    /// <summary>Login de desenvolvimento (só aparece quando a API permite).</summary>
    [HttpPost("/login/dev")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> DevLogin(string email, string? name, CancellationToken ct)
    {
        try
        {
            var auth = await api.DevLoginAsync(email, name, ct);
            await UserClaims.SignInAsync(HttpContext, auth);
            return Redirect(HomeFor(auth.User));
        }
        catch (ApiException e)
        {
            return Redirect("/login?error=" + (e.Code == ErrorCodes.UserBlocked ? "blocked" : "login_failed"));
        }
    }

    [HttpPost("/sair")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/login");
    }

    [Route("/erro/{status:int?}")]
    [IgnoreAntiforgeryToken]
    public IActionResult Error(int? status) =>
        View("~/Views/Shared/Error.cshtml", status switch
        {
            404 => "Página não encontrada.",
            429 => "Muitas tentativas em pouco tempo. Aguarde um minuto e tente de novo.",
            _ => null,
        });

    private string CallbackUrl() => $"{Request.Scheme}://{Request.Host}/auth/callback";

    private static string HomeFor(CurrentUserResponse user) =>
        user.Role is UserRole.ADMIN or UserRole.TEACHER ? "/admin"
        : !user.HasTrainData || !user.HasActiveWorkoutPlan ? "/onboarding"
        : "/";
}
