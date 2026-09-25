using System.Globalization;
using FitAi.Contracts;

namespace FitAi.Api.Payments;

/// <summary>Conversões entre os valores do provedor e os enums do domínio; datas sempre viram UTC aqui.</summary>
public static class PaymentMapping
{
    public static string ToBillingType(PaymentMethod method) => method switch
    {
        PaymentMethod.PIX => "PIX",
        PaymentMethod.BOLETO => "BOLETO",
        PaymentMethod.CREDIT_CARD => "CREDIT_CARD",
        _ => throw new ArgumentOutOfRangeException(nameof(method)),
    };

    public static PaymentMethod FromBillingType(string? billingType) => billingType?.ToUpperInvariant() switch
    {
        "BOLETO" => PaymentMethod.BOLETO,
        "CREDIT_CARD" => PaymentMethod.CREDIT_CARD,
        _ => PaymentMethod.PIX,
    };

    public static string ToCycle(BillingCycle cycle) => cycle == BillingCycle.YEARLY ? "YEARLY" : "MONTHLY";

    public static PaymentStatus FromStatus(string? status) => status?.ToUpperInvariant() switch
    {
        "CONFIRMED" => PaymentStatus.CONFIRMED,
        "RECEIVED" or "RECEIVED_IN_CASH" => PaymentStatus.RECEIVED,
        "OVERDUE" => PaymentStatus.OVERDUE,
        "REFUNDED" or "REFUND_REQUESTED" or "CHARGEBACK_REQUESTED" or "CHARGEBACK_DISPUTE" => PaymentStatus.REFUNDED,
        "DELETED" => PaymentStatus.CANCELED,
        _ => PaymentStatus.PENDING,
    };

    /// <summary>
    /// Datas do Asaas chegam como "2026-09-26" ou "2026-09-26 10:00:00", sem fuso: interpretadas como UTC.
    /// (O PostgreSQL/Npgsql só aceita timestamptz em UTC.)
    /// </summary>
    public static DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string[] formats = ["yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ssK", "O"];
        return DateTimeOffset.TryParseExact(value, formats, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed.ToUniversalTime()
            : null;
    }

    public static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
