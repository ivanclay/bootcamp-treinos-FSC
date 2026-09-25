using System.ComponentModel.DataAnnotations;

namespace FitAi.Contracts;

public sealed class CoachChatRequest
{
    [Required, MinLength(1), MaxLength(100)]
    public List<CoachMessage> Messages { get; set; } = [];
}

public sealed class CoachMessage
{
    /// <summary>"user" ou "assistant".</summary>
    [Required, RegularExpression("^(user|assistant)$")]
    public string Role { get; set; } = "user";

    [Required, MaxLength(8000)]
    public string Content { get; set; } = "";
}

public sealed record CoachChatResponse(
    string Message,
    IReadOnlyList<ExerciseVideoResponse> Videos,
    bool WorkoutPlanChanged);

public sealed record ExerciseVideoResponse(string Title, string ChannelTitle, string Url, string ThumbnailUrl);
