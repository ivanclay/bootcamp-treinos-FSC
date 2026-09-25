using System.Security.Claims;
using FitAi.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FitAi.Web.Infrastructure;

public static class UserClaims
{
    public const string AccessToken = "access_token";
    public const string Picture = "picture";
    public const string TeacherName = "teacher_name";

    public static ClaimsPrincipal CreatePrincipal(AuthTokenResponse auth)
    {
        var user = auth.User;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(AccessToken, auth.AccessToken),
        };
        if (user.Image is not null) claims.Add(new Claim(Picture, user.Image));
        if (user.TeacherName is not null) claims.Add(new Claim(TeacherName, user.TeacherName));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    public static Task SignInAsync(HttpContext context, AuthTokenResponse auth) =>
        context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            CreatePrincipal(auth),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = auth.ExpiresAt });

    public static string FirstName(this ClaimsPrincipal user) =>
        (user.Identity?.Name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";

    public static UserRole Role(this ClaimsPrincipal user) =>
        Enum.TryParse<UserRole>(user.FindFirstValue(ClaimTypes.Role), out var role) ? role : UserRole.STUDENT;

    public static bool IsStaff(this ClaimsPrincipal user) => user.Role() is UserRole.ADMIN or UserRole.TEACHER;

    public static bool IsAdmin(this ClaimsPrincipal user) => user.Role() == UserRole.ADMIN;
}
