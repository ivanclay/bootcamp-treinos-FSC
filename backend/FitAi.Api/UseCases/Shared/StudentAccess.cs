using FitAi.Api.Data;
using FitAi.Api.Entities;
using FitAi.Api.Errors;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.Shared;

/// <summary>
/// Regra de acesso da área administrativa: admin gerencia qualquer usuário;
/// professor só os próprios alunos. Fora do escopo responde 404, sem revelar que o usuário existe.
/// </summary>
public static class StudentAccess
{
    public static IQueryable<User> ManagedBy(this IQueryable<User> users, User actor) =>
        actor.Role == UserRole.ADMIN ? users : users.Where(u => u.TeacherId == actor.Id);

    public static async Task<User> GetManagedUserAsync(AppDbContext db, User actor, string userId, CancellationToken ct)
    {
        return await db.Users.ManagedBy(actor).FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("User not found");
    }

    public static async Task<WorkoutPlan> GetManagedPlanAsync(AppDbContext db, User actor, Guid workoutPlanId, CancellationToken ct)
    {
        var plan = await db.WorkoutPlans
            .Include(p => p.User)
            .Include(p => p.WorkoutDays).ThenInclude(d => d.Exercises)
            .FirstOrDefaultAsync(p => p.Id == workoutPlanId, ct);
        var canManage = plan is not null && (actor.Role == UserRole.ADMIN || plan.User.TeacherId == actor.Id);
        return canManage ? plan! : throw new NotFoundException("Workout plan not found");
    }
}
