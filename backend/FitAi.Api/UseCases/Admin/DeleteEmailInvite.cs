using FitAi.Api.Data;
using FitAi.Api.Entities;
using FitAi.Api.Errors;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.Admin;

/// <summary>Cancela um convite ainda não aceito.</summary>
public sealed class DeleteEmailInvite(AppDbContext db)
{
    public sealed record Input(User Actor, Guid InviteId);

    public async Task ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var invite = await db.EmailInvites.FirstOrDefaultAsync(i => i.Id == input.InviteId, ct);
        if (invite is null || (input.Actor.Role != UserRole.ADMIN && invite.TeacherId != input.Actor.Id))
        {
            throw new NotFoundException("Invite not found");
        }
        if (invite.AcceptedAt is not null) throw new ConflictException("Invite was already accepted");
        db.EmailInvites.Remove(invite);
        await db.SaveChangesAsync(ct);
    }
}
