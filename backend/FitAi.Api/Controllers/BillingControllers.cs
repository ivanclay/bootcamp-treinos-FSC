using FitAi.Api.Auth;
using FitAi.Api.Options;
using FitAi.Api.Payments;
using FitAi.Api.UseCases.Billing;
using FitAi.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace FitAi.Api.Controllers;

/// <summary>Assinatura Pro do professor.</summary>
[ApiController]
[Authorize]
[RequireRoles(UserRole.TEACHER)]
[Route("admin/billing")]
[Tags("Billing")]
public sealed class BillingController(ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<BillingStatusResponse> Status([FromServices] GetBillingStatus getBillingStatus, CancellationToken ct) =>
        await getBillingStatus.ExecuteAsync(new GetBillingStatus.Input(await currentUser.GetAsync(ct)), ct);

    /// <summary>Cria (ou reaproveita) a assinatura pendente e devolve a cobrança: QR PIX ou link da fatura (boleto/cartão).</summary>
    [HttpPost("checkout")]
    [EnableRateLimiting(RateLimiting.Auth)]
    public async Task<CheckoutResponse> Checkout(CheckoutRequest request, [FromServices] StartCheckout startCheckout, CancellationToken ct) =>
        await startCheckout.ExecuteAsync(new StartCheckout.Input(await currentUser.GetAsync(ct), request), ct);

    [HttpGet("pending")]
    public async Task<CheckoutResponse> Pending([FromServices] GetPendingCheckout getPendingCheckout, CancellationToken ct) =>
        await getPendingCheckout.ExecuteAsync(new GetPendingCheckout.Input(await currentUser.GetAsync(ct)), ct);

    /// <summary>Cancela a assinatura; o Pro vale até o fim do período pago.</summary>
    [HttpPost("cancel")]
    public async Task<SubscriptionResponse> Cancel([FromServices] CancelSubscription cancelSubscription, CancellationToken ct) =>
        await cancelSubscription.ExecuteAsync(new CancelSubscription.Input(await currentUser.GetAsync(ct)), ct);

    /// <summary>Só em Development com Payments:Provider=Fake: simula o webhook de pagamento recebido.</summary>
    [HttpPost("simulate-payment")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> SimulatePayment([FromServices] SimulatePayment simulatePayment, CancellationToken ct)
    {
        await simulatePayment.ExecuteAsync(new SimulatePayment.Input(await currentUser.GetAsync(ct)), ct);
        return NoContent();
    }
}

/// <summary>Webhook do Asaas (servidor a servidor). Autenticado pelo token do cabeçalho asaas-access-token.</summary>
[ApiController]
[AllowAnonymous]
[Route("webhooks/asaas")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class WebhooksController(IOptions<AsaasOptions> asaasOptions, ILogger<WebhooksController> logger) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(64 * 1024)]
    public async Task<IActionResult> Asaas([FromServices] HandlePaymentWebhook handlePaymentWebhook, CancellationToken ct)
    {
        if (!AsaasWebhook.IsTokenValid(asaasOptions.Value.WebhookToken, Request.Headers["asaas-access-token"]))
        {
            return Unauthorized();
        }

        using var reader = new StreamReader(Request.Body);
        var evt = AsaasWebhook.Map(await reader.ReadToEndAsync(ct));
        if (evt is null) return BadRequest();

        try
        {
            await handlePaymentWebhook.ExecuteAsync(new HandlePaymentWebhook.Input(evt), ct);
            return Ok(); // processado ou ignorado (duplicado/desconhecido)
        }
        catch (Exception e)
        {
            logger.LogError(e, "Webhook {EventId} failed", evt.EventId);
            return StatusCode(StatusCodes.Status500InternalServerError); // o Asaas reenvia
        }
    }
}
