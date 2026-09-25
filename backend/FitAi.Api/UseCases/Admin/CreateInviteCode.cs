using FitAi.Api.Data;
using FitAi.Api.Entities;
using FitAi.Api.Errors;
using FitAi.Api.UseCases.Shared;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.Admin;

public sealed class CreateInviteCode(AppDbContext db, TimeProvider timeProvider)
{
    public sealed record Input(User Actor, CreateInviteCodeRequest Request);

    public async Task<InviteCodeResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var teacherId = input.Actor.Role == UserRole.ADMIN ? input.Request.TeacherId : input.Actor.Id;
        if (string.IsNullOrWhiteSpace(teacherId)) throw new ValidationException("Escolha o professor");
        var teacher = await db.Users.FirstOrDefaultAsync(u => u.Id == teacherId && u.Role == UserRole.TEACHER, ct)
            ?? throw new ValidationException("Professor não encontrado");

        var now = timeProvider.GetUtcNow();
        string code;
        do code = InviteCodeGenerator.Generate();
        while (await db.InviteCodes.AnyAsync(c => c.Code == code, ct));

        var inviteCode = new InviteCode
        {
            Code = code,
            TeacherId = teacher.Id,
            Teacher = teacher,
            ExpiresAt = input.Request.ExpiresInDays is { } days ? now.AddDays(days) : null,
            MaxUses = input.Request.MaxUses,
        };
        db.InviteCodes.Add(inviteCode);
        await db.SaveChangesAsync(ct);
        return inviteCode.ToResponse(now);
    }
}
