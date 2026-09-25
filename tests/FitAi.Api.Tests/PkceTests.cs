using FitAi.Contracts;

namespace FitAi.Api.Tests;

public class PkceTests
{
    [Fact]
    public void RoundTrip_Verifies()
    {
        var verifier = Pkce.CreateVerifier();
        var challenge = Pkce.CreateChallenge(verifier);
        Assert.True(Pkce.IsValidVerifier(verifier));
        Assert.True(Pkce.IsValidChallenge(challenge));
        Assert.True(Pkce.Verify(verifier, challenge));
    }

    [Fact]
    public void Rfc7636_ExampleVector()
    {
        // Apêndice B da RFC 7636.
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            Pkce.CreateChallenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));
    }

    [Fact]
    public void WrongVerifier_Fails()
    {
        var challenge = Pkce.CreateChallenge(Pkce.CreateVerifier());
        Assert.False(Pkce.Verify(Pkce.CreateVerifier(), challenge));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("curto")]
    [InlineData("com espaço com espaço com espaço com espaço com espaço")]
    public void InvalidVerifier_Fails(string? verifier) =>
        Assert.False(Pkce.Verify(verifier, Pkce.CreateChallenge(Pkce.CreateVerifier())));

    [Fact]
    public void EmptyChallenge_NeverVerifies() => Assert.False(Pkce.Verify(Pkce.CreateVerifier(), ""));
}
