using FitAi.Contracts;
using FitAi.Api.Errors;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FitAi.Api.Auth;

/// <summary>Restringe a ação aos papéis informados, consultando o papel atual no banco.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireRolesAttribute(params UserRole[] roles) : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true) return; // [Authorize] responde 401
        var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUser>();
        var user = await currentUser.GetAsync(context.HttpContext.RequestAborted);
        if (!roles.Contains(user.Role)) throw new ForbiddenException();
    }
}

/// <summary>Garante, para toda requisição autenticada, que o usuário existe e não está bloqueado.</summary>
public sealed class ActiveUserFilter(ICurrentUser currentUser) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true) return;
        await currentUser.GetAsync(context.HttpContext.RequestAborted);
    }
}
