using FitAi.Api.Auth;
using FitAi.Api.UseCases.Admin;
using FitAi.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitAi.Api.Controllers;

[ApiController]
[Authorize]
[RequireRoles(UserRole.ADMIN, UserRole.TEACHER)]
[Route("admin")]
[Tags("Admin")]
public sealed class AdminController(ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Métricas do painel (professor: só os próprios alunos).</summary>
    [HttpGet("dashboard")]
    public async Task<AdminDashboardResponse> Dashboard([FromServices] GetAdminDashboard getAdminDashboard, CancellationToken ct) =>
        await getAdminDashboard.ExecuteAsync(new GetAdminDashboard.Input(await currentUser.GetAsync(ct)), ct);

    [HttpGet("users")]
    public async Task<PagedResponse<AdminUserListItemResponse>> ListUsers(
        [FromQuery] string? search,
        [FromQuery] UserRole? role,
        [FromQuery] string? teacherId,
        [FromServices] ListUsers listUsers,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        await listUsers.ExecuteAsync(new ListUsers.Input(await currentUser.GetAsync(ct), search, role, teacherId, page, pageSize), ct);

    [HttpGet("users/{userId}")]
    public async Task<AdminUserDetailResponse> GetUser(string userId, [FromServices] GetUserDetail getUserDetail, CancellationToken ct) =>
        await getUserDetail.ExecuteAsync(new GetUserDetail.Input(await currentUser.GetAsync(ct), userId), ct);

    /// <summary>Admin: papel, professor, bloqueio. Professor: só <c>allowAiWorkoutPlans</c>.</summary>
    [HttpPatch("users/{userId}")]
    public async Task<AdminUserListItemResponse> UpdateUser(
        string userId, UpdateUserRequest request, [FromServices] UpdateUser updateUser, CancellationToken ct) =>
        await updateUser.ExecuteAsync(new UpdateUser.Input(await currentUser.GetAsync(ct), userId, request), ct);

    /// <summary>Monta um plano para o aluno (vira o plano ativo).</summary>
    [HttpPost("users/{userId}/workout-plans")]
    [ProducesResponseType<WorkoutPlanResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateWorkoutPlan(
        string userId, SaveWorkoutPlanRequest request, [FromServices] CreateStudentWorkoutPlan createStudentWorkoutPlan, CancellationToken ct)
    {
        var result = await createStudentWorkoutPlan.ExecuteAsync(
            new CreateStudentWorkoutPlan.Input(await currentUser.GetAsync(ct), userId, request), ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPut("workout-plans/{workoutPlanId:guid}")]
    public async Task<WorkoutPlanResponse> UpdateWorkoutPlan(
        Guid workoutPlanId, SaveWorkoutPlanRequest request, [FromServices] UpdateStudentWorkoutPlan updateStudentWorkoutPlan, CancellationToken ct) =>
        await updateStudentWorkoutPlan.ExecuteAsync(
            new UpdateStudentWorkoutPlan.Input(await currentUser.GetAsync(ct), workoutPlanId, request), ct);

    [HttpPost("workout-plans/{workoutPlanId:guid}/activate")]
    public async Task<WorkoutPlanResponse> ActivateWorkoutPlan(
        Guid workoutPlanId, [FromServices] ActivateStudentWorkoutPlan activateStudentWorkoutPlan, CancellationToken ct) =>
        await activateStudentWorkoutPlan.ExecuteAsync(
            new ActivateStudentWorkoutPlan.Input(await currentUser.GetAsync(ct), workoutPlanId), ct);

    [HttpDelete("workout-plans/{workoutPlanId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteWorkoutPlan(
        Guid workoutPlanId, [FromServices] DeleteStudentWorkoutPlan deleteStudentWorkoutPlan, CancellationToken ct)
    {
        await deleteStudentWorkoutPlan.ExecuteAsync(new DeleteStudentWorkoutPlan.Input(await currentUser.GetAsync(ct), workoutPlanId), ct);
        return NoContent();
    }

    [HttpGet("invites")]
    public async Task<IReadOnlyList<EmailInviteResponse>> ListInvites([FromServices] ListEmailInvites listEmailInvites, CancellationToken ct) =>
        await listEmailInvites.ExecuteAsync(new ListEmailInvites.Input(await currentUser.GetAsync(ct)), ct);

    [HttpPost("invites")]
    [ProducesResponseType<EmailInviteResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateInvite(
        CreateEmailInviteRequest request, [FromServices] CreateEmailInvite createEmailInvite, CancellationToken ct)
    {
        var result = await createEmailInvite.ExecuteAsync(new CreateEmailInvite.Input(await currentUser.GetAsync(ct), request), ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpDelete("invites/{inviteId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteInvite(Guid inviteId, [FromServices] DeleteEmailInvite deleteEmailInvite, CancellationToken ct)
    {
        await deleteEmailInvite.ExecuteAsync(new DeleteEmailInvite.Input(await currentUser.GetAsync(ct), inviteId), ct);
        return NoContent();
    }

    [HttpGet("invite-codes")]
    public async Task<IReadOnlyList<InviteCodeResponse>> ListInviteCodes([FromServices] ListInviteCodes listInviteCodes, CancellationToken ct) =>
        await listInviteCodes.ExecuteAsync(new ListInviteCodes.Input(await currentUser.GetAsync(ct)), ct);

    [HttpPost("invite-codes")]
    [ProducesResponseType<InviteCodeResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateInviteCode(
        CreateInviteCodeRequest request, [FromServices] CreateInviteCode createInviteCode, CancellationToken ct)
    {
        var result = await createInviteCode.ExecuteAsync(new CreateInviteCode.Input(await currentUser.GetAsync(ct), request), ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("invite-codes/{inviteCodeId:guid}/deactivate")]
    public async Task<InviteCodeResponse> DeactivateInviteCode(
        Guid inviteCodeId, [FromServices] DeactivateInviteCode deactivateInviteCode, CancellationToken ct) =>
        await deactivateInviteCode.ExecuteAsync(new DeactivateInviteCode.Input(await currentUser.GetAsync(ct), inviteCodeId), ct);

    [RequireRoles(UserRole.ADMIN)]
    [HttpGet("ai-settings")]
    public Task<AiSettingsResponse> GetAiSettings([FromServices] GetAiSettings getAiSettings, CancellationToken ct) =>
        getAiSettings.ExecuteAsync(ct);

    [RequireRoles(UserRole.ADMIN)]
    [HttpPut("ai-settings")]
    public Task<AiSettingsResponse> UpdateAiSettings(
        UpdateAiSettingsRequest request, [FromServices] UpdateAiSettings updateAiSettings, CancellationToken ct) =>
        updateAiSettings.ExecuteAsync(new UpdateAiSettings.Input(request), ct);
}
