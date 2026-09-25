using System.ClientModel;
using System.ComponentModel;
using System.Text.Json;
using FitAi.Api.Ai;
using FitAi.Api.Data;
using FitAi.Api.Entities;
using FitAi.Api.Errors;
using FitAi.Api.UseCases.Me;
using FitAi.Api.UseCases.WorkoutPlans;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace FitAi.Api.UseCases.Coach;

/// <summary>
/// Conversa com o Coach AI. As tools executam use cases sempre com o usuário autenticado
/// (capturado por closure) — o modelo nunca escolhe de quem são os dados.
/// </summary>
public sealed class SendCoachMessage(
    AppDbContext db,
    AiSettingsStore settingsStore,
    ChatClientFactory chatClientFactory,
    GetUserTrainData getUserTrainData,
    UpsertUserTrainData upsertUserTrainData,
    ListWorkoutPlans listWorkoutPlans,
    CreateWorkoutPlan createWorkoutPlan,
    SearchExerciseVideos searchExerciseVideos,
    ILogger<SendCoachMessage> logger)
{
    public sealed record Input(User User, IReadOnlyList<CoachMessage> Messages);

    public async Task<CoachChatResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var user = input.User;
        var settings = await settingsStore.GetAsync(ct);
        var teacherName = user.TeacherId is null
            ? null
            : await db.Users.Where(u => u.Id == user.TeacherId).Select(u => u.Name).FirstOrDefaultAsync(ct);

        var systemPrompt = CoachPrompt.Build(
            string.IsNullOrWhiteSpace(settings.SystemPrompt) ? CoachPrompt.Default : settings.SystemPrompt,
            settings.UpperBodyCoverImages,
            settings.LowerBodyCoverImages,
            user.AllowAiWorkoutPlans,
            teacherName);

        var videos = new List<ExerciseVideoResponse>();
        var workoutPlanChanged = false;

        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(
                async (CancellationToken token) => await getUserTrainData.ExecuteAsync(new(user.Id), token),
                "getUserTrainData",
                "Busca os dados de treino do usuário autenticado (peso, altura, idade, % gordura). Retorna null se não houver dados cadastrados."),
            AIFunctionFactory.Create(
                async (UpdateTrainDataArgs args, CancellationToken token) =>
                    await upsertUserTrainData.ExecuteAsync(
                        new(user.Id, args.Name, args.WeightInGrams, args.HeightInCentimeters, args.Age, Math.Clamp(args.BodyFatPercentage, 0, 100)),
                        token),
                "updateUserTrainData",
                "Atualiza os dados de treino do usuário autenticado. O peso deve ser em gramas (converter kg * 1000)."),
            AIFunctionFactory.Create(
                async (CancellationToken token) => await listWorkoutPlans.ExecuteAsync(new(user.Id, null), token),
                "getWorkoutPlans",
                "Lista todos os planos de treino do usuário autenticado."),
            AIFunctionFactory.Create(
                async ([Description("Nome do exercício (ex: Remada Curvada)")] string exerciseName, CancellationToken token) =>
                {
                    try
                    {
                        var result = await searchExerciseVideos.ExecuteAsync(new(exerciseName), token);
                        videos.AddRange(result.Videos.Take(1));
                        return result;
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        logger.LogWarning(e, "Exercise video search failed");
                        return new SearchExerciseVideos.Output([], SearchExerciseVideos.BuildSearchUrl(exerciseName));
                    }
                },
                "searchExerciseVideos",
                "Busca vídeos no YouTube mostrando a execução correta de um exercício. Retorna os vídeos encontrados e um link de busca."),
        };

        if (user.AllowAiWorkoutPlans)
        {
            tools.Add(AIFunctionFactory.Create(
                async (CreateWorkoutPlanArgs args, CancellationToken token) =>
                {
                    var plan = await createWorkoutPlan.ExecuteAsync(
                        new(user.Id, args.ToRequest(), WorkoutPlanSource.AI, user.Id), token);
                    workoutPlanChanged = true;
                    return plan;
                },
                "createWorkoutPlan",
                "Cria um novo plano de treino completo para o usuário (7 dias, MONDAY a SUNDAY). O plano anterior é desativado."));
        }

        var messages = new List<ChatMessage> { new(ChatRole.System, systemPrompt) };
        messages.AddRange(input.Messages
            .Where(m => !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => new ChatMessage(m.Role == "assistant" ? ChatRole.Assistant : ChatRole.User, m.Content)));

        using var client = chatClientFactory.Create(settings.Provider, settings.Model);
        ChatResponse response;
        try
        {
            response = await client.GetResponseAsync(messages, new ChatOptions { Tools = tools }, ct);
        }
        catch (ClientResultException e)
        {
            logger.LogError(e, "AI provider {Provider} request failed", settings.Provider);
            throw new ExternalServiceException($"AI provider request failed (status {e.Status})");
        }

        return new CoachChatResponse(response.Text, videos, workoutPlanChanged);
    }

    public sealed class UpdateTrainDataArgs
    {
        [Description("Nome do usuário, se ele informou")]
        public string? Name { get; set; }

        [Description("Peso do usuário em gramas (ex: 70kg = 70000)")]
        public int WeightInGrams { get; set; }

        [Description("Altura do usuário em centímetros")]
        public int HeightInCentimeters { get; set; }

        [Description("Idade do usuário")]
        public int Age { get; set; }

        [Description("Percentual de gordura corporal (inteiro de 0 a 100)")]
        public int BodyFatPercentage { get; set; }
    }

    public sealed class CreateWorkoutPlanArgs
    {
        [Description("Nome do plano de treino")]
        public string Name { get; set; } = "";

        [Description("Objetivo principal do plano de treino")]
        public WorkoutGoal Goal { get; set; }

        [Description("URL da imagem de capa do plano. Se omitida, usa a capa do primeiro dia de treino.")]
        public string? CoverImageUrl { get; set; }

        [Description("Array com exatamente 7 dias de treino (MONDAY a SUNDAY)")]
        public List<DayArgs> WorkoutDays { get; set; } = [];

        public SaveWorkoutPlanRequest ToRequest() => new()
        {
            Name = Name,
            Goal = Goal,
            CoverImageUrl = CoverImageUrl,
            WorkoutDays = WorkoutDays.Select(d => new SaveWorkoutDayRequest
            {
                Name = d.Name,
                WeekDay = d.WeekDay,
                IsRest = d.IsRest,
                EstimatedDurationInSeconds = d.EstimatedDurationInSeconds,
                CoverImageUrl = d.CoverImageUrl,
                Exercises = d.Exercises.Select(e => new SaveWorkoutExerciseRequest
                {
                    Order = e.Order,
                    Name = e.Name,
                    Sets = e.Sets,
                    Reps = e.Reps,
                    RestTimeInSeconds = e.RestTimeInSeconds,
                }).ToList(),
            }).ToList(),
        };
    }

    public sealed class DayArgs
    {
        [Description("Nome do dia (ex: Peito e Tríceps, Descanso)")]
        public string Name { get; set; } = "";

        [Description("Dia da semana")]
        public WeekDay WeekDay { get; set; }

        [Description("Se é dia de descanso (true) ou treino (false)")]
        public bool IsRest { get; set; }

        [Description("Duração estimada em segundos (0 para dias de descanso)")]
        public int EstimatedDurationInSeconds { get; set; }

        [Description("URL da imagem de capa do dia. Usar as URLs de superior ou inferior conforme o foco muscular do dia.")]
        public string? CoverImageUrl { get; set; }

        [Description("Lista de exercícios (vazia para dias de descanso)")]
        public List<ExerciseArgs> Exercises { get; set; } = [];
    }

    public sealed class ExerciseArgs
    {
        [Description("Ordem do exercício no dia")]
        public int Order { get; set; }

        [Description("Nome do exercício")]
        public string Name { get; set; } = "";

        [Description("Número de séries")]
        public int Sets { get; set; }

        [Description("Número de repetições")]
        public int Reps { get; set; }

        [Description("Tempo de descanso entre séries em segundos")]
        public int RestTimeInSeconds { get; set; }
    }
}
