using FitAi.Contracts;
using FitAi.Web.Infrastructure;

namespace FitAi.Web.Areas.Admin.Models;

public sealed record UsersViewModel(
    PagedResponse<AdminUserListItemResponse> Users,
    string? Search,
    UserRole? Role,
    string? TeacherId,
    IReadOnlyList<AdminUserListItemResponse> Teachers);

public sealed record UserDetailViewModel(AdminUserDetailResponse Detail, IReadOnlyList<AdminUserListItemResponse> Teachers);

public sealed record InvitesViewModel(
    IReadOnlyList<EmailInviteResponse> Invites,
    IReadOnlyList<InviteCodeResponse> Codes,
    IReadOnlyList<AdminUserListItemResponse> Teachers);

/// <summary>Formulário do editor de planos: sempre 7 dias (segunda a domingo).</summary>
public sealed class PlanEditorViewModel
{
    public string UserId { get; set; } = "";
    public string StudentName { get; set; } = "";
    public Guid? PlanId { get; set; }
    public string Name { get; set; } = "";
    public WorkoutGoal? Goal { get; set; }
    public string? CoverImageUrl { get; set; }
    public List<DayEditor> Days { get; set; } = [];
    public string? Error { get; set; }

    public static PlanEditorViewModel Empty(string userId, string studentName) => new()
    {
        UserId = userId,
        StudentName = studentName,
        Days = Format.WeekOrder.Select(d => new DayEditor
        {
            WeekDay = d,
            IsRest = d is WeekDay.SATURDAY or WeekDay.SUNDAY,
            Name = d is WeekDay.SATURDAY or WeekDay.SUNDAY ? "Descanso" : "",
            DurationMinutes = 45,
            Exercises = [],
        }).ToList(),
    };

    public static PlanEditorViewModel From(string userId, string studentName, WorkoutPlanResponse plan, bool copy)
    {
        var model = Empty(userId, studentName);
        model.PlanId = copy ? null : plan.Id;
        model.Name = copy ? plan.Name + " (cópia)" : plan.Name;
        model.Goal = plan.Goal;
        model.CoverImageUrl = plan.CoverImageUrl;
        foreach (var editor in model.Days)
        {
            var day = plan.WorkoutDays.FirstOrDefault(d => d.WeekDay == editor.WeekDay);
            editor.Included = day is not null;
            if (day is null) continue;
            editor.Name = day.Name;
            editor.IsRest = day.IsRest;
            editor.DurationMinutes = Math.Max(0, day.EstimatedDurationInSeconds / 60);
            editor.CoverImageUrl = day.CoverImageUrl;
            editor.Exercises = day.Exercises.OrderBy(e => e.Order).Select(e => new ExerciseEditor
            {
                Name = e.Name, Sets = e.Sets, Reps = e.Reps, RestTimeInSeconds = e.RestTimeInSeconds,
            }).ToList();
        }
        return model;
    }

    public SaveWorkoutPlanRequest ToRequest() => new()
    {
        Name = Name?.Trim() ?? "",
        Goal = Goal,
        CoverImageUrl = string.IsNullOrWhiteSpace(CoverImageUrl) ? null : CoverImageUrl.Trim(),
        WorkoutDays = Days.Where(d => d.Included).Select(d => new SaveWorkoutDayRequest
        {
            Name = string.IsNullOrWhiteSpace(d.Name) ? (d.IsRest ? "Descanso" : Format.DayName(d.WeekDay)) : d.Name.Trim(),
            WeekDay = d.WeekDay,
            IsRest = d.IsRest,
            EstimatedDurationInSeconds = d.IsRest ? 0 : d.DurationMinutes * 60,
            CoverImageUrl = string.IsNullOrWhiteSpace(d.CoverImageUrl) ? null : d.CoverImageUrl.Trim(),
            Exercises = d.IsRest
                ? []
                : d.Exercises.Where(e => !string.IsNullOrWhiteSpace(e.Name)).Select((e, i) => new SaveWorkoutExerciseRequest
                {
                    Order = i, Name = e.Name!.Trim(), Sets = e.Sets, Reps = e.Reps, RestTimeInSeconds = e.RestTimeInSeconds,
                }).ToList(),
        }).ToList(),
    };
}

public sealed class DayEditor
{
    public WeekDay WeekDay { get; set; }
    public bool Included { get; set; } = true;
    public string? Name { get; set; }
    public bool IsRest { get; set; }
    public int DurationMinutes { get; set; }
    public string? CoverImageUrl { get; set; }
    public List<ExerciseEditor> Exercises { get; set; } = [];
}

public sealed class ExerciseEditor
{
    public string? Name { get; set; }
    public int Sets { get; set; } = 3;
    public int Reps { get; set; } = 12;
    public int RestTimeInSeconds { get; set; } = 60;
}

public sealed class AiSettingsForm
{
    public string Provider { get; set; } = "";
    public string? Model { get; set; }
    public string? SystemPrompt { get; set; }
    public string? UpperBodyCoverImages { get; set; }
    public string? LowerBodyCoverImages { get; set; }
}
