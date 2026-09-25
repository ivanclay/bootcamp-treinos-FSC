using System.Security.Cryptography;

namespace FitAi.Api.UseCases.Shared;

public static class InviteCodeGenerator
{
    // Sem 0/O, 1/I/L, para facilitar a digitação.
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    public static string Generate() => "FIT-" + RandomNumberGenerator.GetString(Alphabet, 6);

    public static string Normalize(string code) => code.Trim().ToUpperInvariant();
}
