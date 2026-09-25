using System.ComponentModel.DataAnnotations;

namespace FitAi.Contracts;

/// <summary>
/// URL de imagem de capa: absoluta (http/https) ou caminho relativo à API (ex.: /covers/upper-1.jpg).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CoverImageUrlAttribute() : ValidationAttribute("URL de imagem inválida.")
{
    public static bool IsValid(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || (value.StartsWith('/') && !value.StartsWith("//") && !value.Contains(".."))
        || (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https");

    public override bool IsValid(object? value) => IsValid(value as string);
}
