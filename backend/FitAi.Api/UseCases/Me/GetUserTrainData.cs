using FitAi.Api.Data;
using FitAi.Api.UseCases.Shared;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.Me;

/// <summary>Dados físicos do usuário; nulo enquanto algum deles não foi informado.</summary>
public sealed class GetUserTrainData(AppDbContext db)
{
    public sealed record Input(string UserId);

    public async Task<UserTrainDataResponse?> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == input.UserId, ct);
        return user?.ToTrainData();
    }
}
