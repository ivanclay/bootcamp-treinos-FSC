using FitAi.App.Services;

namespace FitAi.App;

public partial class App : Application
{
    private readonly AppShell _shell;

    public App(AppShell shell, ApiClient api, AuthService auth)
    {
        InitializeComponent();
        // O visual segue o Figma (tema claro).
        UserAppTheme = AppTheme.Light;
        _shell = shell;

        api.SessionExpired += (_, _) => MainThread.BeginInvokeOnMainThread(async () =>
        {
            auth.Logout();
            await Shell.Current.GoToAsync("//login");
        });
    }

    protected override Window CreateWindow(IActivationState? activationState) => new(_shell);
}
