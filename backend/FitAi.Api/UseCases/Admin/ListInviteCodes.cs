using FitAi.Api.Data;
using FitAi.Api.Entities;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.Admin;

public sealed class ListInviteCodes(AppDbContext db, TimeProvider timeProvider)
{
    public sealed record Input(User Actor);

    public async Task<IReadOnlyList<InviteCodeResponse>> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var query = db.InviteCodes.AsNoTracking().Include(c => c.Teacher).AsQueryable();
        if (input.Actor.Role != UserRole.ADMIN) query = query.Where(c => c.TeacherId == input.Actor.Id);
        var codes = await query.OrderByDescending(c => c.CreatedAt).ToListAsync(ct);
        var now = timeProvider.GetUtcNow();
        return codes.Select(c => c.ToResponse(now)).ToList();
    }
}

public static class InviteCodeMapping
{
    public static InviteCodeResponse ToResponse(this InviteCode c, DateTimeOffset now) => new(
        c.Id, c.Code, c.TeacherId, c.Teacher.Name, c.ExpiresAt, c.MaxUses, c.UsesCount, c.IsActive, c.IsUsable(now), c.CreatedAt);
}
