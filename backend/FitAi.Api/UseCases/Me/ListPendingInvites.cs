using FitAi.Api.Data;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.Me;

/// <summary>Convites de professor para o e-mail do aluno que ainda aguardam resposta.</summary>
public sealed class ListPendingInvites(AppDbContext db)
{
    public sealed record Input(string UserId);

    public async Task<IReadOnlyList<PendingInviteResponse>> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == input.UserId, ct);
        if (user is null || user.Role != UserRole.STUDENT || user.TeacherId is not null) return [];

        return await db.EmailInvites.AsNoTracking()
            .Where(i => i.Email == user.Email && i.Role == UserRole.STUDENT && i.TeacherId != null
                        && i.AcceptedAt == null && i.DeclinedAt == null && !i.Teacher!.IsBlocked)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new PendingInviteResponse(i.Id, i.TeacherId!, i.Teacher!.Name, i.CreatedAt))
            .ToListAsync(ct);
    }
}
