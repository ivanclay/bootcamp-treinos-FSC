using FitAi.Contracts;

namespace FitAi.Api.Entities;

/// <summary>Assinatura do professor (uma por professor). Guarda só ids do provedor — nunca dados de cartão.</summary>
public class Subscription : IHasTimestamps
{
    public Guid Id { get; set; }
    public string TeacherId { get; set; } = "";
    public User Teacher { get; set; } = null!;
    public PlanType Plan { get; set; } = PlanType.PRO;
    public BillingCycle Cycle { get; set; }
    public PaymentMethod Method { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.PENDING;
    public decimal Price { get; set; }

    /// <summary>Até quando o plano pago vale.</summary>
    public DateTimeOffset? CurrentPeriodEnd { get; set; }

    public DateTimeOffset? CanceledAt { get; set; }
    public string? ProviderCustomerId { get; set; }
    public string? ProviderSubscriptionId { get; set; }
    public List<PaymentRecord> Payments { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Espelho das cobranças do provedor (vindas do webhook ou de consulta).</summary>
public class PaymentRecord : IHasTimestamps
{
    public Guid Id { get; set; }
    public Guid SubscriptionId { get; set; }
    public Subscription Subscription { get; set; } = null!;
    public string ProviderPaymentId { get; set; } = "";
    public decimal Value { get; set; }
    public DateTimeOffset DueDate { get; set; }
    public PaymentStatus Status { get; set; }
    public PaymentMethod Method { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public string? InvoiceUrl { get; set; }
    public string? BankSlipUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsPaid => Status is PaymentStatus.CONFIRMED or PaymentStatus.RECEIVED;
}

/// <summary>Idempotência do webhook: o id do evento é a chave primária.</summary>
public class WebhookEvent
{
    public string Id { get; set; } = "";
    public string Provider { get; set; } = "asaas";
    public string EventType { get; set; } = "";
    public DateTimeOffset ReceivedAt { get; set; }
}

/// <summary>Contador de uso por usuário e mês (ex.: mensagens ao Coach AI).</summary>
public class UsageCounter
{
    public string UserId { get; set; } = "";

    /// <summary>Mês em UTC, formato yyyy-MM.</summary>
    public string Period { get; set; } = "";

    public string Kind { get; set; } = "";
    public int Count { get; set; }
}
