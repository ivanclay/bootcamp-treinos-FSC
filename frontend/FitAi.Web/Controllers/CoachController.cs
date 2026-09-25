using FitAi.Contracts;
using FitAi.Web.Infrastructure;
using FitAi.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitAi.Web.Controllers;

[Authorize]
public sealed class CoachController(ApiClient api) : Controller
{
    [HttpGet("/coach")]
    public IActionResult Index(string? q) => View(new CoachViewModel(false, false, q));

    [HttpGet("/onboarding")]
    public async Task<IActionResult> Onboarding(CancellationToken ct)
    {
        var me = await api.GetCurrentUserAsync(ct);
        return View("Index", new CoachViewModel(true, me.Role == UserRole.STUDENT && me.TeacherId is null, null));
    }

    /// <summary>Chamado via fetch pela tela do chat; devolve a resposta já em HTML.</summary>
    [HttpPost("/coach/enviar")]
    public async Task<IActionResult> Send([FromBody] CoachChatRequest request, CancellationToken ct)
    {
        var response = await api.SendCoachMessageAsync(request, ct);
        return Json(new
        {
            message = response.Message,
            html = Markdown.ToHtml(response.Message),
            videos = response.Videos,
            workoutPlanChanged = response.WorkoutPlanChanged,
        });
    }

    [HttpPost("/coach/codigo")]
    public async Task<IActionResult> RedeemCode([FromBody] RedeemInviteCodeRequest request, CancellationToken ct)
    {
        var link = await api.RedeemInviteCodeAsync(request.Code, ct);
        return Json(new { teacherName = link.TeacherName });
    }
}
