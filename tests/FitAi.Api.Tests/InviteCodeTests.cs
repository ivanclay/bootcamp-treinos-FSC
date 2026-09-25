using FitAi.Api.Entities;
using FitAi.Api.UseCases.Shared;

namespace FitAi.Api.Tests;

public class InviteCodeTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Generate_ProducesReadableCode()
    {
        var code = InviteCodeGenerator.Generate();
        Assert.Matches("^FIT-[2-9A-HJKMNP-Z]{6}$", code);
        Assert.Equal(code, InviteCodeGenerator.Normalize("  " + code.ToLowerInvariant() + " "));
    }

    [Theory]
    [InlineData(true, null, null, 0, true)]
    [InlineData(false, null, null, 0, false)]
    [InlineData(true, -1, null, 0, false)]
    [InlineData(true, 1, null, 0, true)]
    [InlineData(true, null, 2, 2, false)]
    [InlineData(true, null, 2, 1, true)]
    public void IsUsable(bool active, int? expiresInDays, int? maxUses, int uses, bool expected)
    {
        var code = new InviteCode
        {
            IsActive = active,
            ExpiresAt = expiresInDays is { } d ? Now.AddDays(d) : null,
            MaxUses = maxUses,
            UsesCount = uses,
        };
        Assert.Equal(expected, code.IsUsable(Now));
    }
}
