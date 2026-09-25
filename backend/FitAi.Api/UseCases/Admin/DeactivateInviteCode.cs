using FitAi.Api.Data;
using FitAi.Api.Entities;
using FitAi.Api.Errors;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.Admin;

public sealed class DeactivateInviteCode(AppDbContext db, TimeProvider timeProvider)
{
    public sealed record Input(User Actor, Guid InviteCodeId);

    public async Task<InviteCodeResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var code = await db.InviteCodes.Include(c => c.Teacher).FirstOrDefaultAsync(c => c.Id == input.InviteCodeId, ct);
        if (code is null || (input.Actor.Role != UserRole.ADMIN && code.TeacherId != input.Actor.Id))
        {
            throw new NotFoundException("Invite code not found");
        }
        code.IsActive = false;
        await db.SaveChangesAsync(ct);
        return code.ToResponse(timeProvider.GetUtcNow());
    }
}
