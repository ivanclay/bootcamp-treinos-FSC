using System.ComponentModel.DataAnnotations;

namespace FitAi.Contracts;

public sealed record UserTrainDataResponse(
    string UserId,
    string UserName,
    int WeightInGrams,
    int HeightInCentimeters,
    int Age,
    int BodyFatPercentage);

public sealed class UpsertUserTrainDataRequest
{
    [MinLength(1)]
    public string? Name { get; set; }

    [Range(0, int.MaxValue)]
    public int WeightInGrams { get; set; }

    [Range(0, int.MaxValue)]
    public int HeightInCentimeters { get; set; }

    [Range(0, 150)]
    public int Age { get; set; }

    [Range(0, 100)]
    public int BodyFatPercentage { get; set; }
}

public sealed class RedeemInviteCodeRequest
{
    [Required, MinLength(4), MaxLength(32)]
    public string Code { get; set; } = "";
}

public sealed record TeacherLinkResponse(string TeacherId, string TeacherName);

/// <summary>Convite de professor aguardando o aceite do aluno.</summary>
public sealed record PendingInviteResponse(Guid Id, string TeacherId, string TeacherName, DateTimeOffset CreatedAt);
