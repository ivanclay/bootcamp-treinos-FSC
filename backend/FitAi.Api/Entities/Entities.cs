using FitAi.Contracts;

namespace FitAi.Api.Entities;

public interface IHasTimestamps
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
}

public class User : IHasTimestamps
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public bool EmailVerified { get; set; }
    public string? Image { get; set; }
    public UserRole Role { get; set; } = UserRole.STUDENT;
    public bool IsBlocked { get; set; }

    /// <summary>Quando falso, o Coach AI não cria nem altera planos deste aluno.</summary>
    public bool AllowAiWorkoutPlans { get; set; } = true;

    public string? TeacherId { get; set; }
    public User? Teacher { get; set; }
    public List<User> Students { get; set; } = [];

    public int? WeightInGrams { get; set; }
    public int? HeightInCentimeters { get; set; }
    public int? Age { get; set; }

    /// <summary>0 a 100, onde 100 representa 100%.</summary>
    public int? BodyFatPercentage { get; set; }

    public List<WorkoutPlan> WorkoutPlans { get; set; } = [];
    public List<ExternalLogin> ExternalLogins { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class ExternalLogin : IHasTimestamps
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = "";
    public string ProviderKey { get; set; } = "";
    public string UserId { get; set; } = "";
    public User User { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class WorkoutPlan : IHasTimestamps
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string UserId { get; set; } = "";
    public User User { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public WorkoutGoal? Goal { get; set; }
    public string? CoverImageUrl { get; set; }
    public WorkoutPlanSource Source { get; set; }
    public string? CreatedById { get; set; }
    public User? CreatedBy { get; set; }
    public List<WorkoutDay> WorkoutDays { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class WorkoutDay : IHasTimestamps
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public Guid WorkoutPlanId { get; set; }
    public WorkoutPlan WorkoutPlan { get; set; } = null!;
    public bool IsRest { get; set; }
    public WeekDay WeekDay { get; set; }
    public int EstimatedDurationInSeconds { get; set; }
    public string? CoverImageUrl { get; set; }
    public List<WorkoutExercise> Exercises { get; set; } = [];
    public List<WorkoutSession> Sessions { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class WorkoutExercise : IHasTimestamps
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int Order { get; set; }
    public Guid WorkoutDayId { get; set; }
    public WorkoutDay WorkoutDay { get; set; } = null!;
    public int Sets { get; set; }
    public int Reps { get; set; }
    public int RestTimeInSeconds { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class WorkoutSession : IHasTimestamps
{
    public Guid Id { get; set; }
    public Guid WorkoutDayId { get; set; }
    public WorkoutDay WorkoutDay { get; set; } = null!;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Convite por e-mail: aplicado no primeiro login com o e-mail convidado.</summary>
public class EmailInvite : IHasTimestamps
{
    public Guid Id { get; set; }

    /// <summary>Sempre em minúsculas.</summary>
    public string Email { get; set; } = "";

    public UserRole Role { get; set; } = UserRole.STUDENT;

    /// <summary>Professor ao qual o aluno será vinculado (nulo em convites de professor).</summary>
    public string? TeacherId { get; set; }
    public User? Teacher { get; set; }

    public string InvitedById { get; set; } = "";
    public User InvitedBy { get; set; } = null!;
    public DateTimeOffset? AcceptedAt { get; set; }
    public string? AcceptedByUserId { get; set; }

    /// <summary>Aluno recusou o convite.</summary>
    public DateTimeOffset? DeclinedAt { get; set; }

    public bool IsPending => AcceptedAt is null && DeclinedAt is null;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class InviteCode : IHasTimestamps
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string TeacherId { get; set; } = "";
    public User Teacher { get; set; } = null!;
    public DateTimeOffset? ExpiresAt { get; set; }
    public int? MaxUses { get; set; }
    public int UsesCount { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsUsable(DateTimeOffset now) =>
        IsActive && (ExpiresAt is null || ExpiresAt > now) && (MaxUses is null || UsesCount < MaxUses);
}

/// <summary>Código de uso único entregue ao cliente após o login externo, trocado por um JWT.</summary>
public class AuthCode
{
    public Guid Id { get; set; }

    /// <summary>SHA-256 do código em hexadecimal; o código em si nunca é salvo.</summary>
    public string CodeHash { get; set; } = "";

    public string UserId { get; set; } = "";
    public User User { get; set; } = null!;

    /// <summary>Hash PKCE (S256) enviado pelo cliente ao iniciar o login.</summary>
    public string CodeChallenge { get; set; } = "";

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class AppSetting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
}
