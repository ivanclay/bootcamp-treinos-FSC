using FitAi.App.Services;
using FitAi.App.ViewModels;
using FitAi.App.Views;
using Microsoft.Extensions.Logging;

namespace FitAi.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<ApiClient>();
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<AppShell>();

        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<PlanViewModel>();
        builder.Services.AddTransient<WorkoutDayViewModel>();
        builder.Services.AddSingleton<CoachViewModel>(); // mantém a conversa ao trocar de aba
        builder.Services.AddTransient<StatsViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();

        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<PlanPage>();
        builder.Services.AddTransient<WorkoutDayPage>();
        builder.Services.AddTransient<CoachPage>();
        builder.Services.AddTransient<StatsPage>();
        builder.Services.AddTransient<ProfilePage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
