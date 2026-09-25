using FitAi.Api.Data;
using FitAi.Api.Entities;
using FitAi.Api.Errors;
using FitAi.Api.UseCases.Shared;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.Admin;

/// <summary>
/// Admin altera papel, professor e bloqueio. Professor só liga/desliga a criação de planos pela IA
/// dos próprios alunos.
/// </summary>
public sealed class UpdateUser(AppDbContext db)
{
    public sealed record Input(User Actor, string UserId, UpdateUserRequest Changes);

    public async Task<AdminUserListItemResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var actor = input.Actor;
        var changes = input.Changes;
        var user = await StudentAccess.GetManagedUserAsync(db, actor, input.UserId, ct);
        var isAdmin = actor.Role == UserRole.ADMIN;

        if (!isAdmin && (changes.Role is not null || changes.TeacherId is not null || changes.IsBlocked is not null))
        {
            throw new ForbiddenException("Somente administradores podem alterar papel, professor ou bloqueio");
        }
        if (user.Id == actor.Id && (changes.Role is { } r && r != actor.Role || changes.IsBlocked == true))
        {
            throw new ValidationException("Você não pode alterar o próprio papel nem se bloquear");
        }

        if (changes.Role is { } role)
        {
            user.Role = role;
            if (role != UserRole.STUDENT) user.TeacherId = null;
        }

        if (changes.TeacherId is not null)
        {
            if (changes.TeacherId.Length == 0)
            {
                user.TeacherId = null;
            }
            else
            {
                if (user.Role != UserRole.STUDENT) throw new ValidationException("Somente alunos podem ser vinculados a um professor");
                var teacherExists = await db.Users.AnyAsync(u => u.Id == changes.TeacherId && u.Role == UserRole.TEACHER, ct);
                if (!teacherExists) throw new ValidationException("Professor não encontrado");
                user.TeacherId = changes.TeacherId;
            }
        }

        if (changes.IsBlocked is { } blocked) user.IsBlocked = blocked;
        if (changes.AllowAiWorkoutPlans is { } allowAi) user.AllowAiWorkoutPlans = allowAi;

        // Alunos deixam de estar vinculados a quem deixou de ser professor.
        if (changes.Role is { } newRole && newRole != UserRole.TEACHER)
        {
            await db.Users.Where(u => u.TeacherId == user.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.TeacherId, (string?)null), ct);
        }

        await db.SaveChangesAsync(ct);
        return await db.Users.AsNoTracking().Where(u => u.Id == user.Id).Select(AdminProjections.UserListItem).FirstAsync(ct);
    }
}
