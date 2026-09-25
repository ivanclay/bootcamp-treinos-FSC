using FitAi.Api.Data;
using FitAi.Api.Entities;
using FitAi.Api.UseCases.Shared;
using FitAi.Contracts;

namespace FitAi.Api.UseCases.Auth;

public sealed class GetCurrentUser(AppDbContext db)
{
    public sealed record Input(User User);

    public Task<CurrentUserResponse> ExecuteAsync(Input input, CancellationToken ct = default) =>
        input.User.ToCurrentUserResponseAsync(db, ct);
}
