using System.ComponentModel.DataAnnotations;

namespace FitAi.Contracts;

/// <summary>
/// URL de imagem de capa: caminho relativo à API (ex.: /covers/upper-1.jpg) ou absoluta (http/https).
/// URLs absolutas só são aceitas nos hosts de <see cref="AllowedHosts"/> (a API preenche com Covers:AllowedHosts).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CoverImageUrlAttribute() : ValidationAttribute("URL de imagem inválida.")
{
    /// <summary>Hosts externos aceitos (sem diferenciar maiúsculas). Nulo = qualquer host; vazio = só caminhos relativos.</summary>
    public static IReadOnlySet<string>? AllowedHosts { get; set; }

    public static bool IsValid(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || (value.StartsWith('/') && !value.StartsWith("//") && !value.Contains(".."))
        || (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            && (AllowedHosts is not { } hosts || hosts.Contains(uri.Host)));

    public override bool IsValid(object? value) => IsValid(value as string);
}
