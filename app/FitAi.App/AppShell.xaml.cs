using FitAi.App.Services;
using FitAi.App.ViewModels;
using FitAi.App.Views;

namespace FitAi.App;

public partial class AppShell : Shell
{
    private readonly AuthService _auth;

    public AppShell(AuthService auth)
    {
        InitializeComponent();
        _auth = auth;
        Routing.RegisterRoute("workout-day", typeof(WorkoutDayPage));
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        if (await _auth.RestoreAsync() && _auth.User is { } user)
        {
            await LoginViewModel.GoHomeAsync(user);
        }
    }
}
