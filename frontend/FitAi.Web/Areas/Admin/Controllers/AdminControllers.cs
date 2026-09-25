using FitAi.Contracts;
using FitAi.Web.Areas.Admin.Models;
using FitAi.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitAi.Web.Areas.Admin.Controllers;

/// <summary>Base da área administrativa: somente ADMIN e TEACHER (a API também valida).</summary>
[Area("Admin")]
[Authorize(Roles = "ADMIN,TEACHER")]
public abstract class AdminControllerBase(ApiClient api) : Controller
{
    protected ApiClient Api { get; } = api;

    protected async Task<IReadOnlyList<AdminUserListItemResponse>> TeachersAsync(CancellationToken ct) =>
        User.IsAdmin() ? (await Api.ListUsersAsync(null, UserRole.TEACHER, null, 1, 100, ct)).Items : [];

    protected void Flash(string message) => TempData["Flash"] = message;
    protected void FlashError(string message) => TempData["FlashError"] = message;
}

public sealed class DashboardController(ApiClient api) : AdminControllerBase(api)
{
    [HttpGet("/admin")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await Api.GetDashboardAsync(ct));
}

public sealed class UsersController(ApiClient api) : AdminControllerBase(api)
{
    [HttpGet("/admin/usuarios")]
    public async Task<IActionResult> Index(string? q, UserRole? role, string? teacherId, int page = 1, CancellationToken ct = default)
    {
        var users = await Api.ListUsersAsync(q, role, teacherId, page, 20, ct);
        return View(new UsersViewModel(users, q, role, teacherId, await TeachersAsync(ct)));
    }

    [HttpGet("/admin/usuarios/{id}")]
    public async Task<IActionResult> Detail(string id, CancellationToken ct) =>
        View(new UserDetailViewModel(await Api.GetUserAsync(id, ct), await TeachersAsync(ct)));

    [HttpPost("/admin/usuarios/{id}")]
    public async Task<IActionResult> Update(string id, UserRole? role, string? teacherId, bool? isBlocked, bool? allowAiWorkoutPlans, CancellationToken ct)
    {
        var request = User.IsAdmin()
            ? new UpdateUserRequest { Role = role, TeacherId = teacherId ?? "", IsBlocked = isBlocked ?? false, AllowAiWorkoutPlans = allowAiWorkoutPlans ?? false }
            : new UpdateUserRequest { AllowAiWorkoutPlans = allowAiWorkoutPlans ?? false };
        try
        {
            await Api.UpdateUserAsync(id, request, ct);
            Flash("Alterações salvas.");
        }
        catch (ApiException e) when (e.Code is ErrorCodes.Validation or ErrorCodes.Conflict or ErrorCodes.Forbidden)
        {
            FlashError(ErrorMessages.For(e));
        }
        return Redirect($"/admin/usuarios/{Uri.EscapeDataString(id)}");
    }
}

public sealed class PlansController(ApiClient api) : AdminControllerBase(api)
{
    [HttpGet("/admin/usuarios/{userId}/planos/novo")]
    public async Task<IActionResult> Create(string userId, Guid? copiarDe, CancellationToken ct)
    {
        var detail = await Api.GetUserAsync(userId, ct);
        var source = copiarDe is null ? null : detail.WorkoutPlans.FirstOrDefault(p => p.Id == copiarDe);
        var model = source is null
            ? PlanEditorViewModel.Empty(userId, detail.User.Name)
            : PlanEditorViewModel.From(userId, detail.User.Name, source, copy: true);
        return View("Editor", model);
    }

    [HttpGet("/admin/usuarios/{userId}/planos/{planId:guid}/editar")]
    public async Task<IActionResult> Edit(string userId, Guid planId, CancellationToken ct)
    {
        var detail = await Api.GetUserAsync(userId, ct);
        var plan = detail.WorkoutPlans.FirstOrDefault(p => p.Id == planId);
        if (plan is null) return NotFound();
        return View("Editor", PlanEditorViewModel.From(userId, detail.User.Name, plan, copy: false));
    }

    [HttpPost("/admin/usuarios/{userId}/planos/salvar")]
    public async Task<IActionResult> Save(string userId, PlanEditorViewModel model, CancellationToken ct)
    {
        model.UserId = userId;
        try
        {
            if (model.PlanId is { } planId) await Api.UpdateStudentPlanAsync(planId, model.ToRequest(), ct);
            else await Api.CreateStudentPlanAsync(userId, model.ToRequest(), ct);
            Flash(model.PlanId is null ? "Plano criado e ativado para o aluno." : "Plano atualizado.");
            return Redirect($"/admin/usuarios/{Uri.EscapeDataString(userId)}");
        }
        catch (ApiException e) when (e.Code == ErrorCodes.Validation)
        {
            model.Error = e.Message;
            return View("Editor", model);
        }
    }

    [HttpPost("/admin/planos/{planId:guid}/ativar")]
    public async Task<IActionResult> Activate(Guid planId, string userId, CancellationToken ct)
    {
        await Api.ActivateStudentPlanAsync(planId, ct);
        Flash("Plano ativado.");
        return Redirect($"/admin/usuarios/{Uri.EscapeDataString(userId)}");
    }

    [HttpPost("/admin/planos/{planId:guid}/excluir")]
    public async Task<IActionResult> Delete(Guid planId, string userId, CancellationToken ct)
    {
        await Api.DeleteStudentPlanAsync(planId, ct);
        Flash("Plano excluído.");
        return Redirect($"/admin/usuarios/{Uri.EscapeDataString(userId)}");
    }
}

public sealed class InvitesController(ApiClient api) : AdminControllerBase(api)
{
    [HttpGet("/admin/convites")]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(new InvitesViewModel(await Api.ListInvitesAsync(ct), await Api.ListInviteCodesAsync(ct), await TeachersAsync(ct)));

    [HttpPost("/admin/convites")]
    public async Task<IActionResult> CreateInvite(string email, UserRole role, string? teacherId, CancellationToken ct)
    {
        try
        {
            var invite = await Api.CreateInviteAsync(new CreateEmailInviteRequest { Email = email, Role = role, TeacherId = teacherId }, ct);
            Flash(invite.AcceptedAt is not null
                ? $"{invite.Email} já tinha conta e foi vinculado na hora."
                : $"Convite criado. Quando {invite.Email} entrar com o Google, o vínculo é feito automaticamente.");
        }
        catch (ApiException e) when (e.Code is ErrorCodes.Validation or ErrorCodes.Conflict or ErrorCodes.Forbidden)
        {
            FlashError(ErrorMessages.For(e));
        }
        return Redirect("/admin/convites");
    }

    [HttpPost("/admin/convites/{id:guid}/cancelar")]
    public async Task<IActionResult> CancelInvite(Guid id, CancellationToken ct)
    {
        try { await Api.DeleteInviteAsync(id, ct); Flash("Convite cancelado."); }
        catch (ApiException e) when (e.Code == ErrorCodes.Conflict) { FlashError(ErrorMessages.For(e)); }
        return Redirect("/admin/convites");
    }

    [HttpPost("/admin/codigos")]
    public async Task<IActionResult> CreateCode(int? expiresInDays, int? maxUses, string? teacherId, CancellationToken ct)
    {
        try
        {
            var code = await Api.CreateInviteCodeAsync(new CreateInviteCodeRequest { ExpiresInDays = expiresInDays, MaxUses = maxUses, TeacherId = teacherId }, ct);
            Flash($"Código {code.Code} gerado. Envie para o aluno digitar no app.");
        }
        catch (ApiException e) when (e.Code == ErrorCodes.Validation) { FlashError(ErrorMessages.For(e)); }
        return Redirect("/admin/convites");
    }

    [HttpPost("/admin/codigos/{id:guid}/desativar")]
    public async Task<IActionResult> DeactivateCode(Guid id, CancellationToken ct)
    {
        await Api.DeactivateInviteCodeAsync(id, ct);
        Flash("Código desativado.");
        return Redirect("/admin/convites");
    }
}

[Authorize(Roles = "ADMIN")]
public sealed class AiController(ApiClient api) : AdminControllerBase(api)
{
    [HttpGet("/admin/ia")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await Api.GetAiSettingsAsync(ct));

    [HttpPost("/admin/ia")]
    public async Task<IActionResult> Save(AiSettingsForm form, bool resetPrompt, CancellationToken ct)
    {
        static List<string> Lines(string? text) =>
            (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        try
        {
            await Api.UpdateAiSettingsAsync(new UpdateAiSettingsRequest
            {
                Provider = form.Provider,
                Model = form.Model,
                SystemPrompt = resetPrompt ? null : form.SystemPrompt,
                UpperBodyCoverImages = Lines(form.UpperBodyCoverImages),
                LowerBodyCoverImages = Lines(form.LowerBodyCoverImages),
            }, ct);
            Flash("Configurações do Coach AI salvas.");
        }
        catch (ApiException e) when (e.Code == ErrorCodes.Validation) { FlashError(e.Message); }
        return Redirect("/admin/ia");
    }
}
