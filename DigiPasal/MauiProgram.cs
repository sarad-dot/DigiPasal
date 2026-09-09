using DigiPasal.Services;
using DigiPasal.ViewModels;
using DigiPasal.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

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
#if ANDROID
        builder.ConfigureMauiHandlers(handlers =>
        {
            handlers.AddHandler<Entry, EntryHandler>();

            EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
            {
                handler.PlatformView.Background = null;
            });
        });
#endif

        RegisterServices(builder.Services);

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<DatabaseService>(_ => DatabaseService.Instance);
        services.AddSingleton<BackupService>(_ => BackupService.Instance);
        services.AddSingleton<AuthService>(_ => AuthService.Instance);

        services.AddTransient<MainPage>();
        services.AddTransient<LoginPage>();
        services.AddTransient<RegisterPage>();
        services.AddTransient<ForgetPasswordPage>();
        services.AddTransient<DashboardPage>();
        services.AddTransient<SalePage>();
        services.AddTransient<CartPage>();
        services.AddTransient<SaleSuccessPage>();
        services.AddTransient<SalesHistoryPage>();
        services.AddTransient<SaleDetailPage>();
        services.AddTransient<ProductPage>();
        services.AddTransient<AddEditProductPage>();
        services.AddTransient<CreditPage>();
        services.AddTransient<ProfilePage>();
        services.AddTransient<ChangePasswordPage>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<QuickSalePage>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<RegisterViewModel>();
        services.AddTransient<ForgotPasswordViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ProfileViewModel>();
        services.AddTransient<ChangePasswordViewModel>();
        services.AddTransient<SettingsViewModel>();
    }
}