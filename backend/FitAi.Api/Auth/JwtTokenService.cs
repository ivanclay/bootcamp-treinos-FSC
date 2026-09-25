using System.Security.Claims;
using System.Text;
using FitAi.Api.Entities;
using FitAi.Api.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FitAi.Api.Auth;

public sealed class JwtTokenService(IOptions<AuthOptions> options, TimeProvider timeProvider)
{
    public const string SubjectClaim = JwtRegisteredClaimNames.Sub;

    public static SymmetricSecurityKey CreateKey(string secret) => new(Encoding.UTF8.GetBytes(secret));

    public (string Token, DateTimeOffset ExpiresAt) CreateToken(User user)
    {
        var opts = options.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddDays(opts.TokenLifetimeDays);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = opts.JwtIssuer,
            Audience = opts.JwtAudience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name, user.Name),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ]),
            SigningCredentials = new SigningCredentials(CreateKey(opts.JwtSecret), SecurityAlgorithms.HmacSha256),
        };
        return (new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
    }
}
