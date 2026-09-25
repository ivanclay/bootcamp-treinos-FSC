using FitAi.Api.Billing;
using FitAi.Api.Data;
using FitAi.Api.Errors;
using FitAi.Api.UseCases.Shared;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.Me;

/// <summary>Vincula o aluno ao professor dono do código de convite.</summary>
public sealed class RedeemInviteCode(AppDbContext db, TimeProvider timeProvider, PlanService planService)
{
    public sealed record Input(string UserId, string Code);

    public async Task<TeacherLinkResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == input.UserId, ct)
            ?? throw new NotFoundException("User not found");
        if (user.Role != UserRole.STUDENT)
        {
            throw new ConflictException("Somente alunos podem se vincular a um professor");
        }
        if (user.TeacherId is not null)
        {
            throw new ConflictException("Você já está vinculado a um professor");
        }

        var code = InviteCodeGenerator.Normalize(input.Code);
        var inviteCode = await db.InviteCodes.Include(c => c.Teacher).FirstOrDefaultAsync(c => c.Code == code, ct);
        if (inviteCode is null || !inviteCode.IsUsable(timeProvider.GetUtcNow()) || inviteCode.Teacher.IsBlocked)
        {
            throw new InvalidInviteCodeException("Código de convite inválido ou expirado");
        }

        await planService.EnsureTeacherCanAddStudentAsync(inviteCode.TeacherId, countPendingInvites: false, ct);
        user.TeacherId = inviteCode.TeacherId;
        inviteCode.UsesCount++;
        await db.SaveChangesAsync(ct);

        return new TeacherLinkResponse(inviteCode.TeacherId, inviteCode.Teacher.Name);
    }
}
