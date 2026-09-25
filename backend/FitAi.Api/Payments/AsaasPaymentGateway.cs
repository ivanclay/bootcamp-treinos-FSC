using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FitAi.Api.Options;
using FitAi.Contracts;
using Microsoft.Extensions.Options;

namespace FitAi.Api.Payments;

/// <summary>
/// Cliente da API v3 do Asaas. Log só de método + caminho + status: nunca cabeçalhos (chave) nem corpo (CPF/CNPJ).
/// </summary>
public sealed class AsaasPaymentGateway : IPaymentGateway
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly AsaasOptions _options;
    private readonly ILogger<AsaasPaymentGateway> _logger;

    public AsaasPaymentGateway(HttpClient http, IOptions<AsaasOptions> options, ILogger<AsaasPaymentGateway> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> EnsureCustomerAsync(GatewayCustomer customer, string? existingCustomerId, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(existingCustomerId)) return existingCustomerId;
        var created = await SendAsync<IdResponse>(HttpMethod.Post, "customers", new
        {
            name = customer.Name,
            email = customer.Email,
            cpfCnpj = Documents.OnlyDigits(customer.CpfCnpj),
            externalReference = customer.ExternalReference,
            notificationDisabled = false,
        }, ct);
        return created.Id;
    }

    public async Task<GatewaySubscription> CreateSubscriptionAsync(
        string customerId, decimal value, BillingCycle cycle, PaymentMethod method, DateOnly nextDueDate,
        string description, string externalReference, CancellationToken ct = default)
    {
        var created = await SendAsync<SubscriptionDto>(HttpMethod.Post, "subscriptions", new
        {
            customer = customerId,
            billingType = PaymentMapping.ToBillingType(method),
            value,
            nextDueDate = PaymentMapping.FormatDate(nextDueDate),
            cycle = PaymentMapping.ToCycle(cycle),
            description,
            externalReference,
        }, ct);
        return new GatewaySubscription(created.Id, created.Status ?? "ACTIVE");
    }

    public async Task<IReadOnlyList<GatewayPayment>> ListSubscriptionPaymentsAsync(string subscriptionId, CancellationToken ct = default)
    {
        var list = await SendAsync<ListResponse<PaymentDto>>(
            HttpMethod.Get, $"subscriptions/{Uri.EscapeDataString(subscriptionId)}/payments", null, ct);
        return list.Data.Select(p => new GatewayPayment(
            p.Id,
            p.Value,
            PaymentMapping.ParseDate(p.DueDate) ?? DateTimeOffset.UtcNow,
            p.Status ?? "PENDING",
            p.BillingType ?? "PIX",
            p.InvoiceUrl,
            p.BankSlipUrl,
            PaymentMapping.ParseDate(p.PaymentDate))).ToList();
    }

    public async Task<GatewayPixQrCode> GetPixQrCodeAsync(string paymentId, CancellationToken ct = default)
    {
        var qr = await SendAsync<PixDto>(HttpMethod.Get, $"payments/{Uri.EscapeDataString(paymentId)}/pixQrCode", null, ct);
        return new GatewayPixQrCode(qr.EncodedImage ?? "", qr.Payload ?? "", PaymentMapping.ParseDate(qr.ExpirationDate));
    }

    public Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct = default) =>
        SendAsync<JsonElement>(HttpMethod.Delete, $"subscriptions/{Uri.EscapeDataString(subscriptionId)}", null, ct);

    private void EnsureConfigured()
    {
        // Falha antes de qualquer HTTP, sem expor configuração.
        if (string.IsNullOrWhiteSpace(_options.ApiKey)) throw new PaymentGatewayException("Asaas API key not configured");
        if (_http.BaseAddress is not null) return;

        var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl) ? "https://api-sandbox.asaas.com/v3/" : _options.BaseUrl;
        if (!baseUrl.EndsWith('/')) baseUrl += "/";
        _http.BaseAddress = new Uri(baseUrl);
        _http.Timeout = TimeSpan.FromSeconds(20);
        _http.DefaultRequestHeaders.Add("access_token", _options.ApiKey);
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FitAi", "1.0"));
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        EnsureConfigured();
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            _logger.LogWarning("Asaas {Method} {Path} -> network error", method, path);
            throw new PaymentGatewayException("Asaas unreachable", null, e);
        }

        using (response)
        {
            _logger.LogInformation("Asaas {Method} {Path} -> {Status}", method, path, (int)response.StatusCode);
            if (!response.IsSuccessStatusCode)
            {
                var detail = "HTTP " + (int)response.StatusCode;
                try
                {
                    var error = await response.Content.ReadFromJsonAsync<ErrorResponseDto>(Json, ct);
                    if (error?.Errors is { Count: > 0 } errors) detail = $"{errors[0].Code}: {errors[0].Description}";
                }
                catch (JsonException) { }
                throw new PaymentGatewayException(detail, (int)response.StatusCode);
            }
            return (await response.Content.ReadFromJsonAsync<T>(Json, ct))!;
        }
    }

    private sealed record IdResponse(string Id);
    private sealed record SubscriptionDto(string Id, string? Status);
    private sealed record ListResponse<T>(List<T> Data);
    private sealed record PaymentDto(
        string Id, decimal Value, string? DueDate, string? Status, string? BillingType,
        string? InvoiceUrl, string? BankSlipUrl, string? PaymentDate);
    private sealed record PixDto(string? EncodedImage, string? Payload, string? ExpirationDate);
    private sealed record ErrorResponseDto(List<ErrorItem>? Errors);
    private sealed record ErrorItem(string? Code, string? Description);
}
