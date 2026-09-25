using FitAi.Api.Data;
using FitAi.Api.Errors;
using FitAi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.UseCases.Me;

public sealed class UpsertUserTrainData(AppDbContext db)
{
    public sealed record Input(
        string UserId,
        string? Name,
        int WeightInGrams,
        int HeightInCentimeters,
        int Age,
        int BodyFatPercentage);

    public async Task<UserTrainDataResponse> ExecuteAsync(Input input, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == input.UserId, ct)
            ?? throw new NotFoundException("User not found");

        if (!string.IsNullOrWhiteSpace(input.Name)) user.Name = input.Name.Trim();
        user.WeightInGrams = input.WeightInGrams;
        user.HeightInCentimeters = input.HeightInCentimeters;
        user.Age = input.Age;
        user.BodyFatPercentage = input.BodyFatPercentage;
        await db.SaveChangesAsync(ct);

        return new UserTrainDataResponse(
            user.Id, user.Name, input.WeightInGrams, input.HeightInCentimeters, input.Age, input.BodyFatPercentage);
    }
}
