using FitAi.Contracts;
using FitAi.Web.Infrastructure;
using FitAi.Web.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitAi.Web.Controllers;

[AllowAnonymous]
public sealed class AccountController(ApiClient api) : Controller
{
    [HttpGet("/login")]
    public async Task<IActionResult> Login(string? error, CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated == true) return Redirect("/");
        AuthProvidersResponse providers;
        try { providers = await api.GetAuthProvidersAsync(ct); }
        catch (HttpRequestException) { providers = new AuthProvidersResponse(false, false); error ??= "offline"; }
        return View(new LoginViewModel(providers, error));
    }

    /// <summary>Envia para o login do Google (feito pela API), que volta em /auth/callback com um código.</summary>
    [HttpGet("/login/google")]
    public IActionResult Google() => Redirect(api.GoogleLoginUrl(CallbackUrl()));

    [HttpGet("/auth/callback")]
    public async Task<IActionResult> Callback(string? code, string? error, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code)) return Redirect("/login?error=" + (error ?? "login_failed"));
        try
        {
            var auth = await api.ExchangeCodeAsync(code, ct);
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
        View("~/Views/Shared/Error.cshtml", status == 404 ? "Página não encontrada." : null);

    private string CallbackUrl() => $"{Request.Scheme}://{Request.Host}/auth/callback";

    private static string HomeFor(CurrentUserResponse user) =>
        user.Role is UserRole.ADMIN or UserRole.TEACHER ? "/admin"
        : !user.HasTrainData || !user.HasActiveWorkoutPlan ? "/onboarding"
        : "/";
}
