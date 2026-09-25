using System.ComponentModel.DataAnnotations;

namespace FitAi.Contracts;

public sealed record AdminDashboardResponse(
    int TotalStudents,
    int ActiveStudentsLast7Days,
    int StudentsWithoutPlan,
    int TotalTeachers,
    int ActiveWorkoutPlans,
    int SessionsStartedLast30Days,
    int CompletedWorkoutsLast30Days,
    double ConclusionRateLast30Days,
    int PendingInvites,
    IReadOnlyList<DailyCountResponse> CompletedWorkoutsByDay,
    IReadOnlyList<AdminUserListItemResponse> RecentlyActiveStudents);

public sealed record DailyCountResponse(string Date, int Count);

public sealed record AdminUserListItemResponse(
    string Id,
    string Name,
    string Email,
    string? Image,
    UserRole Role,
    string? TeacherId,
    string? TeacherName,
    bool IsBlocked,
    bool AllowAiWorkoutPlans,
    bool HasActiveWorkoutPlan,
    DateTimeOffset? LastWorkoutAt,
    DateTimeOffset CreatedAt);

public sealed record AdminUserDetailResponse(
    AdminUserListItemResponse User,
    UserTrainDataResponse? TrainData,
    int WorkoutStreak,
    int CompletedWorkoutsLast30Days,
    IReadOnlyList<WorkoutPlanResponse> WorkoutPlans,
    IReadOnlyList<AdminSessionResponse> RecentSessions);

public sealed record AdminSessionResponse(
    Guid Id,
    string WorkoutDayName,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public sealed class UpdateUserRequest
{
    /// <summary>Somente admin.</summary>
    public UserRole? Role { get; set; }

    /// <summary>Somente admin. String vazia remove o vínculo.</summary>
    public string? TeacherId { get; set; }

    /// <summary>Somente admin.</summary>
    public bool? IsBlocked { get; set; }

    public bool? AllowAiWorkoutPlans { get; set; }
}

public sealed record EmailInviteResponse(
    Guid Id,
    string Email,
    UserRole Role,
    string? TeacherId,
    string? TeacherName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AcceptedAt);

public sealed class CreateEmailInviteRequest
{
    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = "";

    /// <summary>STUDENT (padrão) ou TEACHER (somente admin).</summary>
    public UserRole Role { get; set; } = UserRole.STUDENT;

    /// <summary>Admin pode convidar um aluno para um professor específico.</summary>
    public string? TeacherId { get; set; }
}

public sealed record InviteCodeResponse(
    Guid Id,
    string Code,
    string TeacherId,
    string TeacherName,
    DateTimeOffset? ExpiresAt,
    int? MaxUses,
    int UsesCount,
    bool IsActive,
    bool IsUsable,
    DateTimeOffset CreatedAt);

public sealed class CreateInviteCodeRequest
{
    [Range(1, 365)]
    public int? ExpiresInDays { get; set; }

    [Range(1, 1000)]
    public int? MaxUses { get; set; }

    /// <summary>Admin pode gerar um código em nome de um professor.</summary>
    public string? TeacherId { get; set; }
}

public sealed record AiSettingsResponse(
    string Provider,
    string Model,
    string SystemPrompt,
    bool IsCustomSystemPrompt,
    IReadOnlyList<string> UpperBodyCoverImages,
    IReadOnlyList<string> LowerBodyCoverImages,
    IReadOnlyList<AiProviderResponse> AvailableProviders);

public sealed record AiProviderResponse(string Name, string Type, string DefaultModel, bool IsConfigured);

public sealed class UpdateAiSettingsRequest
{
    [Required]
    public string Provider { get; set; } = "";

    [MaxLength(100)]
    public string? Model { get; set; }

    /// <summary>Nulo ou vazio volta ao prompt padrão.</summary>
    [MaxLength(20000)]
    public string? SystemPrompt { get; set; }

    public List<string> UpperBodyCoverImages { get; set; } = [];

    public List<string> LowerBodyCoverImages { get; set; } = [];
}
