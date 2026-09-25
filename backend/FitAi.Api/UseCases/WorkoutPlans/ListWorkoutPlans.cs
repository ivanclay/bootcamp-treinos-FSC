using FitAi.Api.Data;
using FitAi.Api.UseCases.Shared;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.WorkoutPlans;

public sealed class ListWorkoutPlans(AppDbContext db)
{
    public sealed record Input(string UserId, bool? Active);

    public async Task<IReadOnlyList<WorkoutPlanResponse>> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var query = db.WorkoutPlans.AsNoTracking().Where(p => p.UserId == input.UserId);
        if (input.Active is { } active) query = query.Where(p => p.IsActive == active);

        var plans = await query
            .Include(p => p.WorkoutDays).ThenInclude(d => d.Exercises)
            .AsSplitQuery()
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
        return plans.Select(p => p.ToResponse()).ToList();
    }
}
