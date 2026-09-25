using FitAi.Contracts;
using FitAi.Web.Infrastructure;
using FitAi.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitAi.Web.Controllers;

[Authorize]
public sealed class ProfileController(ApiClient api) : Controller
{
    [HttpGet("/perfil")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var me = await api.GetCurrentUserAsync(ct);
        var trainData = await api.GetTrainDataAsync(ct);
        var invites = await api.ListPendingInvitesAsync(ct);
        var plan = await api.GetMyPlanAsync(ct);
        ViewData["Nav"] = "profile";
        return View(new ProfileViewModel(me, trainData, invites, plan));
    }

    [HttpGet("/perfil/editar")]
    public async Task<IActionResult> Edit(CancellationToken ct)
    {
        var data = await api.GetTrainDataAsync(ct);
        ViewData["Nav"] = "profile";
        return View(new EditProfileViewModel
        {
            Name = data?.UserName ?? User.Identity?.Name,
            WeightKg = (data?.WeightInGrams ?? 0) / 1000m,
            HeightInCentimeters = data?.HeightInCentimeters ?? 0,
            Age = data?.Age ?? 0,
            BodyFatPercentage = data?.BodyFatPercentage ?? 0,
        });
    }

    [HttpPost("/perfil/editar")]
    public async Task<IActionResult> Edit(EditProfileViewModel model, CancellationToken ct)
    {
        try
        {
            await api.UpsertTrainDataAsync(new UpsertUserTrainDataRequest
            {
                Name = string.IsNullOrWhiteSpace(model.Name) ? null : model.Name,
                WeightInGrams = (int)Math.Round(model.WeightKg * 1000),
                HeightInCentimeters = model.HeightInCentimeters,
                Age = model.Age,
                BodyFatPercentage = model.BodyFatPercentage,
            }, ct);
            TempData["Flash"] = "Dados atualizados.";
            return Redirect("/perfil");
        }
        catch (ApiException e) when (e.Code == ErrorCodes.Validation)
        {
            model.Error = "Confira os valores: gordura corporal vai de 0 a 100 e os demais não podem ser negativos.";
            ViewData["Nav"] = "profile";
            return View(model);
        }
    }

    [HttpPost("/perfil/convites/{id:guid}/aceitar")]
    public async Task<IActionResult> AcceptInvite(Guid id, CancellationToken ct)
    {
        try
        {
            var link = await api.AcceptInviteAsync(id, ct);
            TempData["Flash"] = $"Pronto! Agora você é acompanhado por {link.TeacherName}.";
        }
        catch (ApiException e) when (e.Code is ErrorCodes.Conflict or ErrorCodes.NotFound)
        {
            TempData["FlashError"] = ErrorMessages.For(e);
        }
        return Redirect("/perfil");
    }

    [HttpPost("/perfil/convites/{id:guid}/recusar")]
    public async Task<IActionResult> DeclineInvite(Guid id, CancellationToken ct)
    {
        try
        {
            await api.DeclineInviteAsync(id, ct);
            TempData["Flash"] = "Convite recusado.";
        }
        catch (ApiException e) when (e.Code == ErrorCodes.NotFound)
        {
            TempData["FlashError"] = ErrorMessages.For(e);
        }
        return Redirect("/perfil");
    }

    [HttpPost("/perfil/codigo")]
    public async Task<IActionResult> RedeemCode(string code, CancellationToken ct)
    {
        try
        {
            var link = await api.RedeemInviteCodeAsync(code, ct);
            TempData["Flash"] = $"Pronto! Agora você é acompanhado por {link.TeacherName}.";
        }
        catch (ApiException e) when (e.Code is ErrorCodes.InvalidInviteCode or ErrorCodes.Conflict or ErrorCodes.Validation)
        {
            TempData["FlashError"] = ErrorMessages.For(e);
        }
        return Redirect("/perfil");
    }
}
