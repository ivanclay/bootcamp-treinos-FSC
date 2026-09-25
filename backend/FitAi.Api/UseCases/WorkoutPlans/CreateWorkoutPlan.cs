using FitAi.Api.Data;
using FitAi.Api.Entities;
using FitAi.Api.Errors;
using FitAi.Api.UseCases.Shared;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.WorkoutPlans;

/// <summary>
/// Cria um plano e o torna o plano ativo do aluno, desativando o anterior.
/// Usado pelo Coach AI (origem AI) e pelo professor no painel (origem TEACHER).
/// </summary>
public sealed class CreateWorkoutPlan(AppDbContext db)
{
    public sealed record Input(string UserId, SaveWorkoutPlanRequest Plan, WorkoutPlanSource Source, string CreatedById);

    public async Task<WorkoutPlanResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        WorkoutPlanValidator.Validate(input.Plan);

        var userExists = await db.Users.AnyAsync(u => u.Id == input.UserId, ct);
        if (!userExists) throw new NotFoundException("User not found");

        // Um único SaveChanges: desativar o plano atual e criar o novo acontecem na mesma transação.
        var activePlans = await db.WorkoutPlans.Where(p => p.UserId == input.UserId && p.IsActive).ToListAsync(ct);
        foreach (var activePlan in activePlans) activePlan.IsActive = false;

        var days = input.Plan.WorkoutDays.ToEntities().ToList();
        var plan = new WorkoutPlan
        {
            Name = input.Plan.Name.Trim(),
            UserId = input.UserId,
            Goal = input.Plan.Goal,
            CoverImageUrl = string.IsNullOrWhiteSpace(input.Plan.CoverImageUrl)
                ? days.FirstOrDefault(d => !d.IsRest && d.CoverImageUrl is not null)?.CoverImageUrl
                : input.Plan.CoverImageUrl,
            IsActive = true,
            Source = input.Source,
            CreatedById = input.CreatedById,
            WorkoutDays = days,
        };
        db.WorkoutPlans.Add(plan);
        await db.SaveChangesAsync(ct);

        return plan.ToResponse();
    }
}

public static class WorkoutPlanValidator
{
    public static void Validate(SaveWorkoutPlanRequest plan)
    {
        if (string.IsNullOrWhiteSpace(plan.Name)) throw new ValidationException("Informe o nome do plano");
        if (plan.WorkoutDays.Count == 0) throw new ValidationException("O plano precisa ter pelo menos um dia");

        var duplicated = plan.WorkoutDays.GroupBy(d => d.WeekDay).FirstOrDefault(g => g.Count() > 1);
        if (duplicated is not null) throw new ValidationException($"O dia {duplicated.Key} aparece mais de uma vez");

        foreach (var day in plan.WorkoutDays)
        {
            if (string.IsNullOrWhiteSpace(day.Name)) throw new ValidationException("Informe o nome de cada dia");
            if (!day.IsRest && day.Exercises.Count == 0)
            {
                throw new ValidationException($"O treino '{day.Name}' precisa de pelo menos um exercício");
            }
            if (!day.IsRest && day.EstimatedDurationInSeconds <= 0)
            {
                throw new ValidationException($"Informe a duração do treino '{day.Name}'");
            }
            if (day.Exercises.Any(e => string.IsNullOrWhiteSpace(e.Name) || e.Sets < 1 || e.Reps < 1 || e.RestTimeInSeconds < 0))
            {
                throw new ValidationException($"O treino '{day.Name}' tem um exercício inválido (séries e repetições devem ser maiores que zero)");
            }
        }
    }
}
