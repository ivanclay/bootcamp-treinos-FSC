using System.Net;
using System.Text;
using FitAi.Api.Billing;
using FitAi.Api.Entities;
using FitAi.Api.Options;
using FitAi.Api.Payments;
using FitAi.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace FitAi.Api.Tests;

public class DocumentsTests
{
    [Theory]
    [InlineData("529.982.247-25", true)]
    [InlineData("52998224725", true)]
    [InlineData("11.222.333/0001-81", true)]
    [InlineData("529.982.247-24", false)]
    [InlineData("111.111.111-11", false)]
    [InlineData("00000000000000", false)]
    [InlineData("123", false)]
    [InlineData(null, false)]
    public void ValidatesCheckDigits(string? value, bool expected) => Assert.Equal(expected, Documents.IsValidCpfCnpj(value));

    [Fact]
    public void Mask_HidesAllButLastDigits() => Assert.Equal("*********25", Documents.Mask("529.982.247-25"));
}

public class PlanRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Grace = TimeSpan.FromDays(7);

    private static Subscription Sub(SubscriptionStatus status, int? endInDays) => new()
    {
        Status = status,
        Plan = PlanType.PRO,
        CurrentPeriodEnd = endInDays is { } d ? Now.AddDays(d) : null,
    };

    [Fact] public void NoSubscription_IsFree() => Assert.False(PlanRules.Evaluate(null, Now, Grace).IsPaid);
    [Fact] public void Pending_IsFree() => Assert.False(PlanRules.Evaluate(Sub(SubscriptionStatus.PENDING, null), Now, Grace).IsPaid);
    [Fact] public void Active_WithinPeriod_IsPaid() => Assert.Equal(new PlanEvaluation(true, false, false), PlanRules.Evaluate(Sub(SubscriptionStatus.ACTIVE, 10), Now, Grace));
    [Fact] public void Active_LateRenewal_InGrace() => Assert.Equal(new PlanEvaluation(true, true, false), PlanRules.Evaluate(Sub(SubscriptionStatus.ACTIVE, -3), Now, Grace));
    [Fact] public void Active_AfterGrace_IsFree() => Assert.False(PlanRules.Evaluate(Sub(SubscriptionStatus.ACTIVE, -8), Now, Grace).IsPaid);
    [Fact] public void PastDue_InGrace() => Assert.Equal(new PlanEvaluation(true, true, false), PlanRules.Evaluate(Sub(SubscriptionStatus.PAST_DUE, -1), Now, Grace));
    [Fact] public void PastDue_AfterGrace_IsFree() => Assert.False(PlanRules.Evaluate(Sub(SubscriptionStatus.PAST_DUE, -10), Now, Grace).IsPaid);
    [Fact] public void Canceled_BeforeEnd_StillPaid() => Assert.Equal(new PlanEvaluation(true, false, true), PlanRules.Evaluate(Sub(SubscriptionStatus.CANCELED, 5), Now, Grace));
    [Fact] public void Canceled_AfterEnd_IsFree() => Assert.False(PlanRules.Evaluate(Sub(SubscriptionStatus.CANCELED, -1), Now, Grace).IsPaid);

    [Fact]
    public void ExtendPeriod_FromLaterOfCurrentEndAndDueDate()
    {
        Assert.Equal(Now.AddDays(10).AddMonths(1), PlanRules.ExtendPeriod(Now.AddDays(10), Now, Now, BillingCycle.MONTHLY));
        Assert.Equal(Now.AddYears(1), PlanRules.ExtendPeriod(null, Now.AddDays(-2), Now, BillingCycle.YEARLY));
    }
}

public class AsaasWebhookTests
{
    [Theory]
    [InlineData("segredo", "segredo", true)]
    [InlineData("segredo", "outro", false)]
    [InlineData("segredo", "", false)]
    [InlineData("", "", false)]
    [InlineData(null, "qualquer", false)]
    public void Token(string? configured, string? provided, bool expected) =>
        Assert.Equal(expected, AsaasWebhook.IsTokenValid(configured, provided));

    [Fact]
    public void Map_PaymentEvent_DatesBecomeUtc()
    {
        var evt = AsaasWebhook.Map("""
            { "id": "evt_1", "event": "PAYMENT_RECEIVED",
              "payment": { "id": "pay_1", "subscription": "sub_1", "value": 29.9, "dueDate": "2026-09-26",
                           "paymentDate": "2026-09-26", "billingType": "PIX", "status": "RECEIVED" } }
            """)!;
        Assert.Equal(("evt_1", "PAYMENT_RECEIVED", "sub_1", "pay_1"), (evt.EventId, evt.Event, evt.SubscriptionId, evt.PaymentId));
        Assert.Equal(29.9m, evt.Value);
        Assert.Equal(new DateTimeOffset(2026, 9, 26, 0, 0, 0, TimeSpan.Zero), evt.DueDate);
        Assert.Equal(TimeSpan.Zero, evt.DueDate!.Value.Offset);
    }

    [Fact]
    public void Map_SubscriptionEvent()
    {
        var evt = AsaasWebhook.Map("""{ "id": "evt_2", "event": "SUBSCRIPTION_DELETED", "subscription": { "id": "sub_9" } }""")!;
        Assert.Equal("sub_9", evt.SubscriptionId);
        Assert.Null(evt.PaymentId);
    }

    [Theory]
    [InlineData("nao-json")]
    [InlineData("{}")]
    [InlineData("""{ "event": "PAYMENT_RECEIVED" }""")]
    public void Map_Invalid_ReturnsNull(string body) => Assert.Null(AsaasWebhook.Map(body));

    [Theory]
    [InlineData("RECEIVED_IN_CASH", PaymentStatus.RECEIVED)]
    [InlineData("CONFIRMED", PaymentStatus.CONFIRMED)]
    [InlineData("OVERDUE", PaymentStatus.OVERDUE)]
    [InlineData("REFUND_REQUESTED", PaymentStatus.REFUNDED)]
    [InlineData("AWAITING_RISK_ANALYSIS", PaymentStatus.PENDING)]
    public void StatusMapping(string asaas, PaymentStatus expected) => Assert.Equal(expected, PaymentMapping.FromStatus(asaas));
}

public class AsaasPaymentGatewayTests
{
    private sealed class RecordingHandler(HttpStatusCode status, string response) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, string? ApiKey, string? Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.Method, request.RequestUri!.PathAndQuery,
                request.Headers.TryGetValues("access_token", out var v) ? v.Single() : null,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(ct)));
            return new HttpResponseMessage(status) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }

    private static (AsaasPaymentGateway Gateway, RecordingHandler Handler) Create(HttpStatusCode status, string response, string apiKey = "chave-teste")
    {
        var handler = new RecordingHandler(status, response);
        var gateway = new AsaasPaymentGateway(new HttpClient(handler),
            Microsoft.Extensions.Options.Options.Create(new AsaasOptions { ApiKey = apiKey, BaseUrl = "https://api-sandbox.asaas.com/v3" }),
            NullLogger<AsaasPaymentGateway>.Instance);
        return (gateway, handler);
    }

    [Fact]
    public async Task CreateSubscription_SendsExpectedRequest()
    {
        var (gateway, handler) = Create(HttpStatusCode.OK, """{ "id": "sub_123", "status": "ACTIVE" }""");
        var sub = await gateway.CreateSubscriptionAsync("cus_1", 29.90m, BillingCycle.MONTHLY, PaymentMethod.PIX,
            new DateOnly(2026, 9, 25), "FIT.AI Pro (mensal)", "teacher-1");

        Assert.Equal("sub_123", sub.Id);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/v3/subscriptions", request.Path);
        Assert.Equal("chave-teste", request.ApiKey);
        Assert.Contains("\"billingType\":\"PIX\"", request.Body);
        Assert.Contains("\"nextDueDate\":\"2026-09-25\"", request.Body);
        Assert.Contains("\"cycle\":\"MONTHLY\"", request.Body);
        Assert.Contains("\"value\":29.90", request.Body);
    }

    [Fact]
    public async Task EnsureCustomer_SendsOnlyDigits_AndReusesExisting()
    {
        var (gateway, handler) = Create(HttpStatusCode.OK, """{ "id": "cus_9" }""");
        Assert.Equal("cus_9", await gateway.EnsureCustomerAsync(new GatewayCustomer("Paulo", "p@x.com", "529.982.247-25", "t1"), null));
        Assert.Contains("\"cpfCnpj\":\"52998224725\"", handler.Requests[0].Body);
        Assert.Equal("cus_existente", await gateway.EnsureCustomerAsync(new GatewayCustomer("Paulo", "p@x.com", "529.982.247-25", "t1"), "cus_existente"));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ListPayments_ParsesDatesAsUtc()
    {
        var (gateway, handler) = Create(HttpStatusCode.OK, """
            { "data": [ { "id": "pay_1", "value": 299.0, "dueDate": "2026-09-26", "status": "PENDING",
                          "billingType": "BOLETO", "invoiceUrl": "https://x/i/1", "bankSlipUrl": "https://x/b/1" } ] }
            """);
        var payment = Assert.Single(await gateway.ListSubscriptionPaymentsAsync("sub/../1"));
        Assert.Equal("/v3/subscriptions/sub%2F..%2F1/payments", handler.Requests[0].Path);
        Assert.Equal(new DateTimeOffset(2026, 9, 26, 0, 0, 0, TimeSpan.Zero), payment.DueDate);
        Assert.Equal("https://x/i/1", payment.InvoiceUrl);
    }

    [Fact]
    public async Task Error4xx_BecomesGatewayException_WithCodeAndStatus()
    {
        var (gateway, _) = Create(HttpStatusCode.BadRequest, """{ "errors": [ { "code": "invalid_cpfCnpj", "description": "CPF inválido" } ] }""");
        var e = await Assert.ThrowsAsync<PaymentGatewayException>(() =>
            gateway.EnsureCustomerAsync(new GatewayCustomer("P", "p@x.com", "52998224725", "t"), null));
        Assert.Equal(400, e.StatusCode);
        Assert.Contains("invalid_cpfCnpj", e.Message);
    }

    [Fact]
    public async Task MissingApiKey_ThrowsWithoutHttpCall()
    {
        var (gateway, handler) = Create(HttpStatusCode.OK, "{}", apiKey: "");
        await Assert.ThrowsAsync<PaymentGatewayException>(() => gateway.CancelSubscriptionAsync("sub_1"));
        Assert.Empty(handler.Requests);
    }
}
