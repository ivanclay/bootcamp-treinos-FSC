using FitAi.Api.Ai;

namespace FitAi.Api.Tests;

public class CoachPromptTests
{
    [Fact]
    public void Build_IncludesCoverImages_AndTeacher()
    {
        var prompt = CoachPrompt.Build(CoachPrompt.Default, ["https://up/1"], ["https://low/1"], true, "Paulo");
        Assert.Contains("- https://up/1", prompt);
        Assert.Contains("- https://low/1", prompt);
        Assert.Contains("professor Paulo", prompt);
        Assert.DoesNotContain("DESATIVADAS", prompt);
    }

    [Fact]
    public void Build_WhenAiPlansDisabled_AddsRestriction()
    {
        var prompt = CoachPrompt.Build(CoachPrompt.Default, [], [], false, null);
        Assert.Contains("DESATIVADAS", prompt);
    }
}
