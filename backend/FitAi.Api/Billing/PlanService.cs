using FitAi.Api.Data;
using FitAi.Api.Entities;
using FitAi.Api.Errors;
using FitAi.Api.Options;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FitAi.Api.Billing;

public sealed record EffectivePlan(
    PlanType Plan,
    PlanLimitsResponse Limits,
    string? SponsorName,
    bool InGracePeriod,
    bool CancelsAtPeriodEnd);

/// <summary>
/// Plano efetivo e limites. O professor assina; o aluno herda o plano do professor. Admin não tem limites.
/// Voltar ao gratuito não apaga nada: só bloqueia passar dos limites.
/// </summary>
public sealed class PlanService(AppDbContext db, IOptions<PlanOptions> options, TimeProvider timeProvider)
{
    public const string CoachMessagesKind = "coach-messages";

    public static readonly PlanLimitsResponse Unlimited = new(null, null, null, null);

    public PlanLimitsResponse FreeLimits
    {
        get
        {
            var free = options.Value.Free;
            return new PlanLimitsResponse(free.MaxStudents, free.CoachMessagesPerMonth, free.AiPlansPerMonth, free.HistoryDays);
        }
    }

    public TimeSpan GracePeriod => TimeSpan.FromDays(options.Value.Pro.GracePeriodDays);

    public string CurrentPeriod => timeProvider.GetUtcNow().ToString("yyyy-MM");

    public DateTimeOffset MonthStart
    {
        get
        {
            var now = timeProvider.GetUtcNow();
            return new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        }
    }

    public async Task<EffectivePlan> GetForUserAsync(User user, CancellationToken ct)
    {
        if (user.Role == UserRole.ADMIN) return new EffectivePlan(PlanType.PRO, Unlimited, null, false, false);

        var payerId = user.Role == UserRole.TEACHER ? user.Id : user.TeacherId;
        if (payerId is null) return new EffectivePlan(PlanType.FREE, FreeLimits, null, false, false);

        var payer = user.Role == UserRole.TEACHER
            ? new { user.Name, user.IsBlocked, user.Role }
            : await db.Users.AsNoTracking().Where(u => u.Id == payerId).Select(u => new { u.Name, u.IsBlocked, u.Role }).FirstOrDefaultAsync(ct);
        if (payer is null || payer.IsBlocked) return new EffectivePlan(PlanType.FREE, FreeLimits, null, false, false);
        if (payer.Role == UserRole.ADMIN) return new EffectivePlan(PlanType.PRO, Unlimited, payer.Name, false, false);

        var subscription = await db.Subscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.TeacherId == payerId, ct);
        var evaluation = PlanRules.Evaluate(subscription, timeProvider.GetUtcNow(), GracePeriod);
        return evaluation.IsPaid
            ? new EffectivePlan(PlanType.PRO, Unlimited, payer.Name, evaluation.InGracePeriod, evaluation.CancelsAtPeriodEnd)
            : new EffectivePlan(PlanType.FREE, FreeLimits, payer.Name, false, false);
    }

    public async Task<EffectivePlan> GetForUserIdAsync(string userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("User not found");
        return await GetForUserAsync(user, ct);
    }

    /// <summary>Professor pode receber mais um aluno? (conta alunos vinculados e, opcionalmente, convites pendentes)</summary>
    public async Task EnsureTeacherCanAddStudentAsync(string teacherId, bool countPendingInvites, CancellationToken ct)
    {
        var teacher = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == teacherId, ct)
            ?? throw new NotFoundException("Teacher not found");
        var plan = await GetForUserAsync(teacher, ct);
        if (plan.Limits.MaxStudents is not { } max) return;

        var students = await db.Users.CountAsync(u => u.TeacherId == teacherId, ct);
        var pending = 0;
        if (countPendingInvites)
        {
            pending = await db.EmailInvites.CountAsync(
                i => i.TeacherId == teacherId && i.Role == UserRole.STUDENT && i.AcceptedAt == null && i.DeclinedAt == null, ct);
        }
        if (students + pending >= max)
        {
            throw new PlanLimitException(
                $"O plano gratuito permite até {max} alunos por professor (com convites pendentes). Assine o Pro para ter alunos ilimitados.");
        }
    }

    /// <summary>
    /// Registra uma mensagem ao Coach AI no mês. Atômico: no plano gratuito só incrementa se ainda houver saldo.
    /// </summary>
    public async Task ConsumeCoachMessageAsync(User user, CancellationToken ct)
    {
        var plan = await GetForUserAsync(user, ct);
        var limit = plan.Limits.CoachMessagesPerMonth ?? int.MaxValue;
        var period = CurrentPeriod;
        var updated = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "UsageCounters" ("UserId", "Period", "Kind", "Count") VALUES ({user.Id}, {period}, {CoachMessagesKind}, 1)
            ON CONFLICT ("UserId", "Period", "Kind") DO UPDATE SET "Count" = "UsageCounters"."Count" + 1
            WHERE "UsageCounters"."Count" < {limit}
            """, ct);
        if (updated == 0)
        {
            throw new PlanLimitException(
                $"Você usou as {limit} mensagens do Coach AI deste mês no plano gratuito. " +
                (user.TeacherId is null
                    ? "Vincule-se a um professor com o plano Pro para conversar sem limite."
                    : "Peça ao seu professor para assinar o Pro e conversar sem limite."));
        }
    }

    /// <summary>Devolve a mensagem quando a chamada à IA falhou (não conta contra o limite).</summary>
    public Task RefundCoachMessageAsync(User user, CancellationToken ct)
    {
        var period = CurrentPeriod;
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "UsageCounters" SET "Count" = "Count" - 1
            WHERE "UserId" = {user.Id} AND "Period" = {period} AND "Kind" = {CoachMessagesKind} AND "Count" > 0
            """, ct);
    }

    public async Task<int> GetCoachMessagesUsedAsync(string userId, CancellationToken ct)
    {
        var period = CurrentPeriod;
        return await db.UsageCounters.Where(u => u.UserId == userId && u.Period == period && u.Kind == CoachMessagesKind)
            .Select(u => u.Count).FirstOrDefaultAsync(ct);
    }

    public Task<int> CountAiPlansThisMonthAsync(string userId, CancellationToken ct)
    {
        var monthStart = MonthStart;
        return db.WorkoutPlans.CountAsync(p => p.UserId == userId && p.Source == WorkoutPlanSource.AI && p.CreatedAt >= monthStart, ct);
    }
}
