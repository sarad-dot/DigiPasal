using DigiPasal.Services;
using DigiPasal.ViewModels;
using DigiPasal.Views;
using Microsoft.Extensions.Logging;

namespace DigiPasal;

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

        RegisterServices(builder.Services);

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<AuthService>(_ => AuthService.Instance);

        services.AddTransient<MainPage>();
        services.AddTransient<LoginPage>();
        services.AddTransient<RegisterPage>();
        services.AddTransient<ForgetPasswordPage>();
        services.AddTransient<DashboardPage>();
        services.AddTransient<SalePage>();
        services.AddTransient<ProductPage>();
        services.AddTransient<CreditPage>();
        services.AddTransient<ProfilePage>();
        services.AddTransient<ChangePasswordPage>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<RegisterViewModel>();
        services.AddTransient<ForgotPasswordViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ProfileViewModel>();
        services.AddTransient<ChangePasswordViewModel>();
    }
}