using FitAi.Api.Auth;
using FitAi.Api.Data;
using FitAi.Api.Errors;
using FitAi.Api.UseCases.Shared;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.Auth;

/// <summary>Emite um JWT direto para um usuário (usado pelo dev-login).</summary>
public sealed class IssueAuthToken(AppDbContext db, JwtTokenService tokens)
{
    public sealed record Input(string UserId);

    public async Task<AuthTokenResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == input.UserId, ct)
            ?? throw new NotFoundException("User not found");
        if (user.IsBlocked) throw new UserBlockedException();
        var (token, expiresAt) = tokens.CreateToken(user);
        return new AuthTokenResponse(token, expiresAt, await user.ToCurrentUserResponseAsync(db, ct));
    }
}
