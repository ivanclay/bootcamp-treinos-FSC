using FitAi.Api.Data;
using FitAi.Api.Entities;
using FitAi.Api.Errors;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.Admin;

/// <summary>
/// Convite por e-mail. Professor convida alunos para si; admin convida professores ou alunos para um professor.
/// Se a pessoa já tem conta, o convite é aplicado na hora.
/// </summary>
public sealed class CreateEmailInvite(AppDbContext db, TimeProvider timeProvider)
{
    public sealed record Input(User Actor, CreateEmailInviteRequest Invite);

    public async Task<EmailInviteResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var actor = input.Actor;
        var email = input.Invite.Email.Trim().ToLowerInvariant();
        var role = input.Invite.Role;
        var isAdmin = actor.Role == UserRole.ADMIN;

        if (role == UserRole.ADMIN) throw new ValidationException("Administradores são definidos na configuração da aplicação");
        if (role == UserRole.TEACHER && !isAdmin) throw new ForbiddenException("Somente administradores podem convidar professores");

        string? teacherId = null;
        if (role == UserRole.STUDENT)
        {
            teacherId = isAdmin ? input.Invite.TeacherId : actor.Id;
            if (string.IsNullOrWhiteSpace(teacherId)) throw new ValidationException("Escolha o professor");
            var teacherExists = await db.Users.AnyAsync(u => u.Id == teacherId && u.Role == UserRole.TEACHER, ct);
            if (!teacherExists) throw new ValidationException("Professor não encontrado");
        }

        var pendingDuplicate = await db.EmailInvites.AnyAsync(
            i => i.Email == email && i.AcceptedAt == null && i.Role == role && i.TeacherId == teacherId, ct);
        if (pendingDuplicate) throw new ConflictException("Já existe um convite pendente para este e-mail");

        var invite = new EmailInvite { Email = email, Role = role, TeacherId = teacherId, InvitedById = actor.Id };

        var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (existingUser is not null)
        {
            if (role == UserRole.TEACHER)
            {
                if (existingUser.Role == UserRole.STUDENT) { existingUser.Role = UserRole.TEACHER; existingUser.TeacherId = null; }
            }
            else
            {
                if (existingUser.Role != UserRole.STUDENT) throw new ConflictException("Este usuário não é aluno");
                if (existingUser.TeacherId is not null && existingUser.TeacherId != teacherId)
                {
                    throw new ConflictException("Este aluno já está vinculado a outro professor");
                }
                existingUser.TeacherId = teacherId;
            }
            invite.AcceptedAt = timeProvider.GetUtcNow();
            invite.AcceptedByUserId = existingUser.Id;
        }

        db.EmailInvites.Add(invite);
        await db.SaveChangesAsync(ct);

        var teacherName = teacherId is null ? null : await db.Users.Where(u => u.Id == teacherId).Select(u => u.Name).FirstAsync(ct);
        return new EmailInviteResponse(invite.Id, invite.Email, invite.Role, teacherId, teacherName, invite.CreatedAt, invite.AcceptedAt);
    }
}
