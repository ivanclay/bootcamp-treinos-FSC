namespace FitAi.Contracts;

/// <summary>Validação de CPF/CNPJ com dígitos verificadores.</summary>
public static class Documents
{
    public static string OnlyDigits(string? value) => new((value ?? "").Where(char.IsAsciiDigit).ToArray());

    public static bool IsValidCpfCnpj(string? value)
    {
        var digits = OnlyDigits(value);
        return digits.Length switch
        {
            11 => IsValidCpf(digits),
            14 => IsValidCnpj(digits),
            _ => false,
        };
    }

    /// <summary>Mostra só o final do documento (para telas e logs).</summary>
    public static string Mask(string? value)
    {
        var digits = OnlyDigits(value);
        return digits.Length < 4 ? "***" : new string('*', digits.Length - 2) + digits[^2..];
    }

    private static bool IsValidCpf(string d)
    {
        if (d.Distinct().Count() == 1) return false;
        return CheckDigit(d[..9], [10, 9, 8, 7, 6, 5, 4, 3, 2]) == d[9] - '0'
            && CheckDigit(d[..10], [11, 10, 9, 8, 7, 6, 5, 4, 3, 2]) == d[10] - '0';
    }

    private static bool IsValidCnpj(string d)
    {
        if (d.Distinct().Count() == 1) return false;
        return CheckDigit(d[..12], [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]) == d[12] - '0'
            && CheckDigit(d[..13], [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]) == d[13] - '0';
    }

    private static int CheckDigit(string digits, int[] weights)
    {
        var sum = digits.Select((c, i) => (c - '0') * weights[i]).Sum();
        var rest = sum % 11;
        return rest < 2 ? 0 : 11 - rest;
    }
}
