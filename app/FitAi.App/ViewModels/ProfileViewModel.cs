using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitAi.App.Services;
using FitAi.Contracts;

namespace FitAi.App.ViewModels;

public sealed record InviteItem(Guid Id, string Title, string Description);

public partial class ProfileViewModel(ApiClient api, AuthService auth) : BaseViewModel
{
    /// <summary>Convites de professor aguardando o aceite do aluno.</summary>
    public ObservableCollection<InviteItem> Invites { get; } = [];

    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string Subtitle { get; set; } = "";
    [ObservableProperty] public partial string? Image { get; set; }
    [ObservableProperty] public partial string Initial { get; set; } = "";
    [ObservableProperty] public partial bool HasData { get; set; }
    [ObservableProperty] public partial string Weight { get; set; } = "—";
    [ObservableProperty] public partial string Height { get; set; } = "—";
    [ObservableProperty] public partial string BodyFat { get; set; } = "—";
    [ObservableProperty] public partial string Age { get; set; } = "—";
    [ObservableProperty] public partial bool ShowInvite { get; set; }
    [ObservableProperty] public partial string InviteCode { get; set; } = "";
    [ObservableProperty] public partial bool IsEditing { get; set; }
    [ObservableProperty] public partial string EditWeight { get; set; } = "";
    [ObservableProperty] public partial string EditHeight { get; set; } = "";
    [ObservableProperty] public partial string EditBodyFat { get; set; } = "";
    [ObservableProperty] public partial string EditAge { get; set; } = "";
    [ObservableProperty] public partial string? Message { get; set; }
    [ObservableProperty] public partial bool IsFreePlan { get; set; }
    [ObservableProperty] public partial string PlanUsage { get; set; } = "";

    public bool HasMessage => Message is not null;

    partial void OnMessageChanged(string? value) => OnPropertyChanged(nameof(HasMessage));

    public bool HasImage => !string.IsNullOrEmpty(Image);
    public bool HasNoImage => !HasImage;
    public bool IsNotEditing => !IsEditing;

    public string EditButtonText => IsEditing ? "Cancelar" : "Editar dados";

    partial void OnImageChanged(string? value) { OnPropertyChanged(nameof(HasImage)); OnPropertyChanged(nameof(HasNoImage)); }
    partial void OnIsEditingChanged(bool value) { OnPropertyChanged(nameof(IsNotEditing)); OnPropertyChanged(nameof(EditButtonText)); }

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        var user = await auth.RefreshUserAsync();
        var data = await api.GetTrainDataAsync();
        var plan = await api.GetMyPlanAsync();
        var planName = plan.Plan == PlanType.PRO ? "Plano Pro" : "Plano Básico";
        Name = user.Name;
        // Assinatura é feita pelo professor na Web; o app só mostra o plano (política da Google Play).
        Subtitle = planName + (user.TeacherName is not null ? $" · Professor: {user.TeacherName}"
            : user.Role switch { UserRole.ADMIN => " · Admin", UserRole.TEACHER => " · Professor", _ => "" });
        IsFreePlan = plan.Plan == PlanType.FREE && plan.Limits.CoachMessagesPerMonth is not null;
        PlanUsage = plan.Limits.CoachMessagesPerMonth is { } coachLimit
            ? $"Coach AI este mês: {Math.Min(plan.CoachMessagesUsedThisMonth, coachLimit)} de {coachLimit} mensagens"
              + (plan.Limits.AiPlansPerMonth is { } aiLimit ? $"\nPlanos montados pela IA: {Math.Min(plan.AiPlansCreatedThisMonth, aiLimit)} de {aiLimit}" : "")
            : "";
        Image = user.Image;
        Initial = user.Name.Length > 0 ? user.Name[..1].ToUpperInvariant() : "?";
        ShowInvite = user is { Role: UserRole.STUDENT, TeacherId: null };
        HasData = data is not null;
        Weight = data is null ? "—" : Format.Kg(data.WeightInGrams);
        Height = data?.HeightInCentimeters.ToString() ?? "—";
        BodyFat = data is null ? "—" : $"{data.BodyFatPercentage}%";
        Age = data?.Age.ToString() ?? "—";
        EditWeight = data is null ? "" : (data.WeightInGrams / 1000.0).ToString("0.#", CultureInfo.InvariantCulture);
        EditHeight = data?.HeightInCentimeters.ToString() ?? "";
        EditBodyFat = data?.BodyFatPercentage.ToString() ?? "";
        EditAge = data?.Age.ToString() ?? "";

        Invites.Clear();
        foreach (var invite in await api.ListPendingInvitesAsync())
        {
            Invites.Add(new InviteItem(invite.Id, $"{invite.TeacherName} quer ser seu professor",
                $"Ao aceitar, {invite.TeacherName} poderá ver seus dados, planos e treinos, e montar planos para você."));
        }
    });

    [RelayCommand]
    private Task AcceptInviteAsync(InviteItem invite) => RunAsync(async () =>
    {
        var link = await api.AcceptInviteAsync(invite.Id);
        IsBusy = false;
        await LoadAsync();
        Message = $"Pronto! Agora você é acompanhado por {link.TeacherName}.";
    });

    [RelayCommand]
    private Task DeclineInviteAsync(InviteItem invite) => RunAsync(async () =>
    {
        await api.DeclineInviteAsync(invite.Id);
        Invites.Remove(invite);
        Message = "Convite recusado.";
    });

    [RelayCommand]
    private void ToggleEdit() => IsEditing = !IsEditing;

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        if (!double.TryParse(EditWeight.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var kg)
            || !int.TryParse(EditHeight, out var height) || !int.TryParse(EditAge, out var age)
            || !int.TryParse(EditBodyFat, out var bodyFat) || bodyFat is < 0 or > 100)
        {
            ErrorMessage = "Confira os valores: gordura corporal vai de 0 a 100.";
            return;
        }
        await api.UpsertTrainDataAsync(new UpsertUserTrainDataRequest
        {
            WeightInGrams = (int)Math.Round(kg * 1000),
            HeightInCentimeters = height,
            Age = age,
            BodyFatPercentage = bodyFat,
        });
        IsEditing = false;
        IsBusy = false;
        await LoadAsync();
        Message = "Dados atualizados.";
    });

    [RelayCommand]
    private Task RedeemInviteAsync() => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(InviteCode)) return;
        var link = await api.RedeemInviteCodeAsync(InviteCode.Trim());
        InviteCode = "";
        IsBusy = false;
        await LoadAsync();
        Message = $"Pronto! Agora você é acompanhado por {link.TeacherName}.";
    });

    [RelayCommand]
    private async Task LogoutAsync()
    {
        auth.Logout();
        await Shell.Current.GoToAsync("//login");
    }
}
