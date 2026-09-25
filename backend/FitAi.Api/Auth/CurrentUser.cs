using System.Security.Claims;
using FitAi.Api.Data;
using FitAi.Api.Entities;
using FitAi.Api.Errors;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.Auth;

/// <summary>
/// Usuário autenticado na requisição. Papel e bloqueio são sempre lidos do banco
/// (não do token), então promover ou bloquear alguém vale imediatamente.
/// </summary>
public interface ICurrentUser
{
    string UserId { get; }
    Task<User> GetAsync(CancellationToken ct = default);
}

public sealed class CurrentUser(IHttpContextAccessor accessor, AppDbContext db) : ICurrentUser
{
    private User? _user;

    public string UserId =>
        accessor.HttpContext?.User.FindFirstValue(JwtTokenService.SubjectClaim)
        ?? throw new UnauthorizedException();

    public async Task<User> GetAsync(CancellationToken ct = default)
    {
        if (_user is not null) return _user;
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == UserId, ct)
            ?? throw new UnauthorizedException();
        if (user.IsBlocked) throw new UserBlockedException();
        return _user = user;
    }
}
