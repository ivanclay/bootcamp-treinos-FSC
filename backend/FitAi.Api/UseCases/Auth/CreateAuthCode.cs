using System.Security.Cryptography;
using System.Text;
using FitAi.Api.Data;
using FitAi.Api.Entities;

namespace FitAi.Api.UseCases.Auth;

/// <summary>Gera o código de uso único (2 minutos) que o cliente troca por um JWT.</summary>
public sealed class CreateAuthCode(AppDbContext db, TimeProvider timeProvider)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    public sealed record Input(string UserId, string CodeChallenge);

    public sealed record Output(string Code);

    public async Task<Output> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        db.AuthCodes.Add(new AuthCode
        {
            CodeHash = Hash(code),
            UserId = input.UserId,
            CodeChallenge = input.CodeChallenge,
            ExpiresAt = timeProvider.GetUtcNow().Add(Lifetime),
        });
        await db.SaveChangesAsync(ct);
        return new Output(code);
    }

    public static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code))).ToLowerInvariant();
}
