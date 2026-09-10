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
        // Remove Android Material underlines from all text/picker inputs app-wide
        static void RemoveAndroidUnderline(Android.Views.View? platformView)
        {
            if (platformView is null)
                return;

            platformView.Background = null;
            platformView.SetBackgroundColor(Android.Graphics.Color.Transparent);
            platformView.BackgroundTintList =
                Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
        }

        EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) =>
            RemoveAndroidUnderline(handler.PlatformView));

        EditorHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) =>
            RemoveAndroidUnderline(handler.PlatformView));

        PickerHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) =>
            RemoveAndroidUnderline(handler.PlatformView));

        DatePickerHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) =>
            RemoveAndroidUnderline(handler.PlatformView));

        TimePickerHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) =>
            RemoveAndroidUnderline(handler.PlatformView));

        SearchBarHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) =>
        {
            RemoveAndroidUnderline(handler.PlatformView);
            if (handler.PlatformView is Android.Views.ViewGroup group)
            {
                for (var i = 0; i < group.ChildCount; i++)
                {
                    if (group.GetChildAt(i) is Android.Widget.EditText editText)
                        RemoveAndroidUnderline(editText);
                }
            }
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
        services.AddTransient<CreditDetailPage>();
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