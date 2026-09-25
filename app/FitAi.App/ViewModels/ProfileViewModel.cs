using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitAi.App.Services;
using FitAi.Contracts;

namespace FitAi.App.ViewModels;

public partial class ProfileViewModel(ApiClient api, AuthService auth) : BaseViewModel
{
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
        Name = user.Name;
        Subtitle = user.TeacherName is not null ? $"Professor: {user.TeacherName}"
            : user.Role switch { UserRole.ADMIN => "Admin", UserRole.TEACHER => "Professor", _ => "Aluno" };
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
