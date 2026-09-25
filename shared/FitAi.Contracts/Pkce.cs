using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace FitAi.Contracts;

/// <summary>
/// PKCE (RFC 7636, método S256) para o login: o cliente cria um segredo (verifier), envia só o hash (challenge)
/// ao iniciar o login e apresenta o segredo ao trocar o código pelo token. Assim, um código interceptado
/// (ex.: outro app registrando fitai://) ou injetado no navegador de outra pessoa (login CSRF) não serve para nada.
/// </summary>
public static partial class Pkce
{
    public static string CreateVerifier() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string CreateChallenge(string verifier) => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    public static bool IsValidVerifier(string? verifier) => verifier is not null && VerifierRegex().IsMatch(verifier);

    public static bool IsValidChallenge(string? challenge) => challenge is not null && ChallengeRegex().IsMatch(challenge);

    public static bool Verify(string? verifier, string? challenge)
    {
        if (!IsValidVerifier(verifier) || !IsValidChallenge(challenge)) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(CreateChallenge(verifier!)),
            Encoding.ASCII.GetBytes(challenge!));
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [GeneratedRegex("^[A-Za-z0-9._~-]{43,128}$")]
    private static partial Regex VerifierRegex();

    [GeneratedRegex("^[A-Za-z0-9_-]{43}$")]
    private static partial Regex ChallengeRegex();
}
