using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FitAi.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<PlanType>))]
public enum PlanType { FREE, PRO }

[JsonConverter(typeof(JsonStringEnumConverter<BillingCycle>))]
public enum BillingCycle { MONTHLY, YEARLY }

[JsonConverter(typeof(JsonStringEnumConverter<PaymentMethod>))]
public enum PaymentMethod { PIX, BOLETO, CREDIT_CARD }

[JsonConverter(typeof(JsonStringEnumConverter<SubscriptionStatus>))]
public enum SubscriptionStatus { PENDING, ACTIVE, PAST_DUE, CANCELED }

[JsonConverter(typeof(JsonStringEnumConverter<PaymentStatus>))]
public enum PaymentStatus { PENDING, CONFIRMED, RECEIVED, OVERDUE, REFUNDED, CANCELED }

/// <summary>Limites de um plano; nulo = ilimitado.</summary>
public sealed record PlanLimitsResponse(int? MaxStudents, int? CoachMessagesPerMonth, int? AiPlansPerMonth, int? HistoryDays);

public sealed record PlanPricesResponse(decimal MonthlyPrice, decimal YearlyPrice);

/// <summary>Plano efetivo de quem está logado (aluno herda o plano do professor).</summary>
public sealed record MyPlanResponse(
    PlanType Plan,
    string? SponsorName,
    PlanLimitsResponse Limits,
    int CoachMessagesUsedThisMonth,
    int AiPlansCreatedThisMonth,
    bool InGracePeriod);

public sealed record PaymentResponse(
    string Id,
    decimal Value,
    DateTimeOffset DueDate,
    PaymentStatus Status,
    PaymentMethod Method,
    DateTimeOffset? PaidAt,
    string? InvoiceUrl,
    string? BankSlipUrl);

public sealed record SubscriptionResponse(
    Guid Id,
    SubscriptionStatus Status,
    BillingCycle Cycle,
    PaymentMethod Method,
    decimal Price,
    DateTimeOffset? CurrentPeriodEnd,
    DateTimeOffset? CanceledAt,
    bool InGracePeriod,
    bool CancelsAtPeriodEnd,
    IReadOnlyList<PaymentResponse> Payments);

public sealed record BillingStatusResponse(
    PlanType EffectivePlan,
    SubscriptionResponse? Subscription,
    int StudentsCount,
    PlanLimitsResponse FreeLimits,
    PlanPricesResponse Prices,
    bool CanSimulatePayments);

public sealed record PixQrCodeResponse(string EncodedImage, string Payload, DateTimeOffset? ExpirationDate);

/// <summary>Cobrança aberta da assinatura: QR PIX (PIX) ou fatura hospedada (boleto e cartão).</summary>
public sealed record CheckoutResponse(SubscriptionResponse Subscription, PaymentResponse Payment, PixQrCodeResponse? Pix);

public sealed class CheckoutRequest
{
    [Required, MinLength(3), MaxLength(120)]
    public string Name { get; set; } = "";

    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = "";

    /// <summary>CPF (11 dígitos) ou CNPJ (14 dígitos), com ou sem pontuação.</summary>
    [Required, MaxLength(20)]
    public string CpfCnpj { get; set; } = "";

    public BillingCycle Cycle { get; set; } = BillingCycle.MONTHLY;

    public PaymentMethod Method { get; set; } = PaymentMethod.PIX;
}
