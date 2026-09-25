using FitAi.Api.Billing;
using FitAi.Api.Data;
using FitAi.Api.Errors;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.Me;

/// <summary>Aluno aceita (vincula-se ao professor) ou recusa um convite enviado para o e-mail dele.</summary>
public sealed class RespondToInvite(AppDbContext db, TimeProvider timeProvider, PlanService planService)
{
    public sealed record Input(string UserId, Guid InviteId, bool Accept);

    public async Task<TeacherLinkResponse?> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == input.UserId, ct)
            ?? throw new NotFoundException("User not found");
        var invite = await db.EmailInvites.Include(i => i.Teacher)
            .FirstOrDefaultAsync(i => i.Id == input.InviteId && i.Email == user.Email && i.Role == UserRole.STUDENT, ct);
        if (invite is null || !invite.IsPending || invite.Teacher is null)
        {
            throw new NotFoundException("Invite not found");
        }

        var now = timeProvider.GetUtcNow();
        if (!input.Accept)
        {
            invite.DeclinedAt = now;
            await db.SaveChangesAsync(ct);
            return null;
        }

        if (user.Role != UserRole.STUDENT) throw new ConflictException("Somente alunos podem se vincular a um professor");
        if (user.TeacherId is not null) throw new ConflictException("Você já está vinculado a um professor");
        if (invite.Teacher.IsBlocked || invite.Teacher.Role != UserRole.TEACHER) throw new NotFoundException("Invite not found");

        await planService.EnsureTeacherCanAddStudentAsync(invite.TeacherId!, countPendingInvites: false, ct);
        user.TeacherId = invite.TeacherId;
        invite.AcceptedAt = now;
        invite.AcceptedByUserId = user.Id;
        await db.SaveChangesAsync(ct);
        return new TeacherLinkResponse(invite.Teacher.Id, invite.Teacher.Name);
    }
}
