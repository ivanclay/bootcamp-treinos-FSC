using FitAi.Api.Auth;
using FitAi.Api.Data;
using FitAi.Api.Errors;
using FitAi.Api.UseCases.Shared;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.Auth;

public sealed class ExchangeAuthCode(AppDbContext db, JwtTokenService tokens, TimeProvider timeProvider)
{
    public sealed record Input(string Code);

    public async Task<AuthTokenResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var hash = CreateAuthCode.Hash(input.Code.Trim());
        var authCode = await db.AuthCodes.Include(c => c.User).FirstOrDefaultAsync(c => c.CodeHash == hash, ct);
        if (authCode is null || authCode.UsedAt is not null || authCode.ExpiresAt <= now)
        {
            throw new UnauthorizedException("Invalid or expired code");
        }

        authCode.UsedAt = now;
        await db.SaveChangesAsync(ct);

        // Limpeza oportunista de códigos vencidos.
        await db.AuthCodes.Where(c => c.ExpiresAt < now.AddDays(-1)).ExecuteDeleteAsync(ct);

        var user = authCode.User;
        if (user.IsBlocked) throw new UserBlockedException();
        var (token, expiresAt) = tokens.CreateToken(user);
        return new AuthTokenResponse(token, expiresAt, await user.ToCurrentUserResponseAsync(db, ct));
    }
}
