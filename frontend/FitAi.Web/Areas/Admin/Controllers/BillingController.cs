using FitAi.Contracts;
using FitAi.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitAi.Web.Areas.Admin.Controllers;

/// <summary>Assinatura Pro do professor. Cartão e boleto são pagos na fatura do Asaas; PIX pelo QR.</summary>
[Authorize(Roles = "TEACHER")]
public sealed class BillingController(ApiClient api) : AdminControllerBase(api)
{
    [HttpGet("/admin/assinatura")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await Api.GetBillingAsync(ct));

    [HttpPost("/admin/assinatura/checkout")]
    public async Task<IActionResult> Checkout(CheckoutRequest request, CancellationToken ct)
    {
        try
        {
            await Api.CheckoutAsync(request, ct);
            return Redirect("/admin/assinatura/pagamento");
        }
        catch (ApiException e) when (e.Code is ErrorCodes.Validation or ErrorCodes.Conflict or ErrorCodes.PaymentProvider or ErrorCodes.RateLimited)
        {
            FlashError(ErrorMessages.For(e));
            return Redirect("/admin/assinatura");
        }
    }

    [HttpGet("/admin/assinatura/pagamento")]
    public async Task<IActionResult> Pending(CancellationToken ct)
    {
        var status = await Api.GetBillingAsync(ct);
        if (status.EffectivePlan == PlanType.PRO) return Redirect("/admin/assinatura");
        try
        {
            ViewData["CanSimulate"] = status.CanSimulatePayments;
            return View(await Api.GetPendingPaymentAsync(ct));
        }
        catch (ApiException e) when (e.Code is ErrorCodes.NotFound or ErrorCodes.PaymentProvider)
        {
            FlashError(e.Code == ErrorCodes.NotFound ? "Nenhuma assinatura aguardando pagamento." : ErrorMessages.For(e));
            return Redirect("/admin/assinatura");
        }
    }

    /// <summary>"Já paguei": só confere o status — quem confirma o pagamento é o webhook do provedor.</summary>
    [HttpPost("/admin/assinatura/conferir")]
    public async Task<IActionResult> CheckPayment(CancellationToken ct)
    {
        var status = await Api.GetBillingAsync(ct);
        if (status.EffectivePlan == PlanType.PRO)
        {
            Flash("Pagamento confirmado! Seu plano Pro está ativo. 🎉");
            return Redirect("/admin/assinatura");
        }
        FlashError("Ainda não recebemos a confirmação. PIX costuma levar alguns segundos; boleto, até 3 dias úteis; cartão, alguns minutos.");
        return Redirect("/admin/assinatura/pagamento");
    }

    [HttpPost("/admin/assinatura/cancelar")]
    public async Task<IActionResult> Cancel(CancellationToken ct)
    {
        try
        {
            var subscription = await Api.CancelSubscriptionAsync(ct);
            Flash(subscription.CurrentPeriodEnd is { } end
                ? $"Assinatura cancelada. O Pro continua valendo até {end.ToLocalTime():dd/MM/yyyy}."
                : "Assinatura cancelada.");
        }
        catch (ApiException e) when (e.Code is ErrorCodes.NotFound or ErrorCodes.PaymentProvider)
        {
            FlashError(ErrorMessages.For(e));
        }
        return Redirect("/admin/assinatura");
    }

    /// <summary>Desenvolvimento (provedor Fake): simula o webhook de pagamento recebido.</summary>
    [HttpPost("/admin/assinatura/simular-pagamento")]
    public async Task<IActionResult> Simulate(CancellationToken ct)
    {
        await Api.SimulatePaymentAsync(ct);
        return await CheckPayment(ct);
    }
}
