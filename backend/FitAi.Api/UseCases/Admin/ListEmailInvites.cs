using FitAi.Api.Data;
using FitAi.Api.Entities;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.Admin;

public sealed class ListEmailInvites(AppDbContext db)
{
    public sealed record Input(User Actor);

    public async Task<IReadOnlyList<EmailInviteResponse>> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var query = db.EmailInvites.AsNoTracking();
        if (input.Actor.Role != UserRole.ADMIN) query = query.Where(i => i.TeacherId == input.Actor.Id);
        return await query
            .OrderBy(i => i.AcceptedAt != null || i.DeclinedAt != null).ThenByDescending(i => i.CreatedAt)
            .Select(i => new EmailInviteResponse(
                i.Id, i.Email, i.Role, i.TeacherId, i.Teacher != null ? i.Teacher.Name : null, i.CreatedAt, i.AcceptedAt, i.DeclinedAt))
            .ToListAsync(ct);
    }
}
