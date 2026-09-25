using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FitAi.App.Services;
using FitAi.Contracts;

namespace FitAi.App.ViewModels;

public partial class LoginViewModel(ApiClient api, AuthService auth) : BaseViewModel
{
    [ObservableProperty]
    public partial bool DevLoginAvailable { get; set; }

    public string HeroImage { get; } = Format.Cover("/covers/login.jpg");

    [ObservableProperty]
    public partial string DevEmail { get; set; } = "";

    [RelayCommand]
    private async Task LoadAsync()
    {
        try { DevLoginAvailable = (await api.GetAuthProvidersAsync()).DevLogin; }
        catch (Exception) { DevLoginAvailable = false; }
    }

    [RelayCommand]
    private Task LoginWithGoogleAsync() => RunAsync(async () =>
    {
        try { await GoHomeAsync(await auth.LoginWithGoogleAsync()); }
        catch (TaskCanceledException) { /* usuário fechou o navegador */ }
    });

    [RelayCommand]
    private Task DevLoginAsync() => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(DevEmail)) return;
        await GoHomeAsync(await auth.DevLoginAsync(DevEmail.Trim()));
    });

    public static Task GoHomeAsync(CurrentUserResponse user) =>
        Shell.Current.GoToAsync(!user.HasTrainData || !user.HasActiveWorkoutPlan ? "//main/coach?onboarding=true" : "//main/home");
}
