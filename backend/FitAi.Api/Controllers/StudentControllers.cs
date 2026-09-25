using FitAi.Api.Auth;
using FitAi.Api.UseCases.Coach;
using FitAi.Api.UseCases.Home;
using FitAi.Api.UseCases.Me;
using FitAi.Api.UseCases.WorkoutPlans;
using FitAi.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitAi.Api.Controllers;

[ApiController]
[Authorize]
[Route("home")]
[Tags("Home")]
public sealed class HomeController(ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Dados da Home: treino de hoje, sequência e consistência da semana.</summary>
    [HttpGet("{date}")]
    public Task<HomeDataResponse> Get(DateOnly date, [FromServices] GetHomeData getHomeData, CancellationToken ct) =>
        getHomeData.ExecuteAsync(new GetHomeData.Input(currentUser.UserId, date), ct);
}

[ApiController]
[Authorize]
[Route("me")]
[Tags("Me")]
public sealed class MeController(ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Dados físicos do usuário (nulo se ainda não informados).</summary>
    [HttpGet]
    public Task<UserTrainDataResponse?> Get([FromServices] GetUserTrainData getUserTrainData, CancellationToken ct) =>
        getUserTrainData.ExecuteAsync(new GetUserTrainData.Input(currentUser.UserId), ct);

    [HttpPut]
    public Task<UserTrainDataResponse> Upsert(
        UpsertUserTrainDataRequest request, [FromServices] UpsertUserTrainData upsertUserTrainData, CancellationToken ct) =>
        upsertUserTrainData.ExecuteAsync(new UpsertUserTrainData.Input(
            currentUser.UserId,
            request.Name,
            request.WeightInGrams,
            request.HeightInCentimeters,
            request.Age,
            request.BodyFatPercentage), ct);

    /// <summary>Vincula o aluno a um professor usando um código de convite.</summary>
    [HttpPost("teacher")]
    public Task<TeacherLinkResponse> RedeemInviteCode(
        RedeemInviteCodeRequest request, [FromServices] RedeemInviteCode redeemInviteCode, CancellationToken ct) =>
        redeemInviteCode.ExecuteAsync(new RedeemInviteCode.Input(currentUser.UserId, request.Code), ct);
}

[ApiController]
[Authorize]
[Route("stats")]
[Tags("Stats")]
public sealed class StatsController(ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Estatísticas de Evolução no intervalo (datas em UTC, formato yyyy-MM-dd).</summary>
    [HttpGet]
    public Task<StatsResponse> Get(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromServices] GetStats getStats, CancellationToken ct) =>
        getStats.ExecuteAsync(new GetStats.Input(currentUser.UserId, from, to), ct);
}

[ApiController]
[Authorize]
[Route("workout-plans")]
[Tags("Workout Plan")]
public sealed class WorkoutPlansController(ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<WorkoutPlanResponse>> List(
        [FromQuery] bool? active, [FromServices] ListWorkoutPlans listWorkoutPlans, CancellationToken ct) =>
        listWorkoutPlans.ExecuteAsync(new ListWorkoutPlans.Input(currentUser.UserId, active), ct);

    /// <summary>Cria um plano para o próprio usuário (torna-se o plano ativo).</summary>
    [HttpPost]
    [ProducesResponseType<WorkoutPlanResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        SaveWorkoutPlanRequest request, [FromServices] CreateWorkoutPlan createWorkoutPlan, CancellationToken ct)
    {
        var result = await createWorkoutPlan.ExecuteAsync(
            new CreateWorkoutPlan.Input(currentUser.UserId, request, WorkoutPlanSource.AI, currentUser.UserId), ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("{workoutPlanId:guid}")]
    public Task<WorkoutPlanSummaryResponse> Get(
        Guid workoutPlanId, [FromServices] GetWorkoutPlan getWorkoutPlan, CancellationToken ct) =>
        getWorkoutPlan.ExecuteAsync(new GetWorkoutPlan.Input(currentUser.UserId, workoutPlanId), ct);

    [HttpGet("{workoutPlanId:guid}/days/{workoutDayId:guid}")]
    public Task<WorkoutDayResponse> GetDay(
        Guid workoutPlanId, Guid workoutDayId, [FromServices] GetWorkoutDay getWorkoutDay, CancellationToken ct) =>
        getWorkoutDay.ExecuteAsync(new GetWorkoutDay.Input(currentUser.UserId, workoutPlanId, workoutDayId), ct);

    /// <summary>Inicia o treino do dia.</summary>
    [HttpPost("{workoutPlanId:guid}/days/{workoutDayId:guid}/sessions")]
    [ProducesResponseType<StartWorkoutSessionResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> StartSession(
        Guid workoutPlanId, Guid workoutDayId, [FromServices] StartWorkoutSession startWorkoutSession, CancellationToken ct)
    {
        var result = await startWorkoutSession.ExecuteAsync(
            new StartWorkoutSession.Input(currentUser.UserId, workoutPlanId, workoutDayId), ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Marca o treino como concluído.</summary>
    [HttpPatch("{workoutPlanId:guid}/days/{workoutDayId:guid}/sessions/{sessionId:guid}")]
    public Task<WorkoutSessionResponse> CompleteSession(
        Guid workoutPlanId,
        Guid workoutDayId,
        Guid sessionId,
        CompleteWorkoutSessionRequest request,
        [FromServices] CompleteWorkoutSession completeWorkoutSession,
        CancellationToken ct) =>
        completeWorkoutSession.ExecuteAsync(new CompleteWorkoutSession.Input(
            currentUser.UserId, workoutPlanId, workoutDayId, sessionId, request.CompletedAt!.Value), ct);
}

[ApiController]
[Authorize]
[Route("coach")]
[Tags("Coach AI")]
public sealed class CoachController(ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Envia a conversa ao Coach AI e devolve a resposta. O cliente guarda o histórico e o reenvia a cada mensagem.
    /// </summary>
    [HttpPost("chat")]
    public async Task<CoachChatResponse> Chat(
        CoachChatRequest request, [FromServices] SendCoachMessage sendCoachMessage, CancellationToken ct) =>
        await sendCoachMessage.ExecuteAsync(new SendCoachMessage.Input(await currentUser.GetAsync(ct), request.Messages), ct);
}
