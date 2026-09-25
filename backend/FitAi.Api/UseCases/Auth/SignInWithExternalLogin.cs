using FitAi.Api.Data;
using FitAi.Api.Entities;
using FitAi.Api.Errors;
using FitAi.Api.Options;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FitAi.Api.UseCases.Auth;

/// <summary>
/// Encontra ou cria o usuário do login externo e aplica papéis e convites:
/// e-mails de <c>Auth:AdminEmails</c> viram ADMIN; convites por e-mail pendentes
/// promovem a professor ou vinculam o aluno ao professor.
/// </summary>
public sealed class SignInWithExternalLogin(AppDbContext db, IOptions<AuthOptions> authOptions, TimeProvider timeProvider)
{
    public sealed record Input(string Provider, string ProviderKey, string Email, string? Name, string? Image, bool EmailVerified);

    public sealed record Output(string UserId);

    public async Task<Output> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var email = input.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email)) throw new ValidationException("Email is required");

        var login = await db.ExternalLogins.Include(l => l.User)
            .FirstOrDefaultAsync(l => l.Provider == input.Provider && l.ProviderKey == input.ProviderKey, ct);
        var user = login?.User ?? await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null)
        {
            user = new User
            {
                Email = email,
                Name = string.IsNullOrWhiteSpace(input.Name) ? email.Split('@')[0] : input.Name.Trim(),
                Image = input.Image,
                EmailVerified = input.EmailVerified,
            };
            db.Users.Add(user);
        }
        else if (user.Image is null && input.Image is not null)
        {
            user.Image = input.Image;
        }

        if (login is null)
        {
            db.ExternalLogins.Add(new ExternalLogin { Provider = input.Provider, ProviderKey = input.ProviderKey, User = user });
        }

        if (user.IsBlocked) throw new UserBlockedException();

        var isConfiguredAdmin = authOptions.Value.AdminEmails.Any(a => string.Equals(a.Trim(), email, StringComparison.OrdinalIgnoreCase));
        if (isConfiguredAdmin) user.Role = UserRole.ADMIN;

        await ApplyPendingInvitesAsync(user, email, ct);

        await db.SaveChangesAsync(ct);
        return new Output(user.Id);
    }

    private async Task ApplyPendingInvitesAsync(User user, string email, CancellationToken ct)
    {
        var invites = await db.EmailInvites
            .Where(i => i.Email == email && i.AcceptedAt == null)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);
        if (invites.Count == 0) return;

        var now = timeProvider.GetUtcNow();
        var teacherInvite = invites.FirstOrDefault(i => i.Role == UserRole.TEACHER);
        if (teacherInvite is not null && user.Role == UserRole.STUDENT)
        {
            user.Role = UserRole.TEACHER;
            user.TeacherId = null;
        }

        var studentInvite = invites.FirstOrDefault(i => i.Role == UserRole.STUDENT && i.TeacherId != null);
        if (studentInvite is not null && user.Role == UserRole.STUDENT && user.TeacherId is null)
        {
            user.TeacherId = studentInvite.TeacherId;
        }

        foreach (var invite in invites)
        {
            invite.AcceptedAt = now;
            invite.AcceptedByUserId = user.Id;
        }
    }
}
