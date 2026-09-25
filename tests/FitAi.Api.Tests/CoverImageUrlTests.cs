using FitAi.Contracts;

namespace FitAi.Api.Tests;

public class CoverImageUrlTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("/covers/upper-1.jpg", true)]
    [InlineData("https://cdn.exemplo.com/foto.jpg", true)]
    [InlineData("//evil.com/x.jpg", false)]
    [InlineData("/covers/../appsettings.json", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("foto.jpg", false)]
    public void Validates(string? value, bool expected) => Assert.Equal(expected, CoverImageUrlAttribute.IsValid(value));
}
