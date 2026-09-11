using DigiPasal.Services;
using DigiPasal.ViewModels;
using Microsoft.Maui.Controls;

namespace DigiPasal.Views;

public partial class SettingsPage : ContentPage
{
    private readonly AuthService _authService;
    private readonly SettingsViewModel _viewModel;

    public SettingsPage(SettingsViewModel viewModel, AuthService authService)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
        _authService = authService;
    }

    protected override async void OnAppearing()
    {
        try
        {
            base.OnAppearing();

            if (!_authService.IsAuthenticated)
            {
                await Shell.Current.GoToAsync("//Login");
                return;
            }

            await _viewModel.LoadAsync();
        }
        catch (Exception)
        {
            throw; // TODO handle exception
        }
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void ShopSettings_Tapped(object? sender, TappedEventArgs e)
        => await NavigationGuard.GoToAsync("ShopSettings");

    private async void Reports_Tapped(object? sender, TappedEventArgs e)
        => await NavigationGuard.GoToAsync("Reports");

    private async void SalesReport_Tapped(object? sender, TappedEventArgs e)
        => await NavigationGuard.GoToAsync("SalesReport");

    private async void StockReport_Tapped(object? sender, TappedEventArgs e)
        => await NavigationGuard.GoToAsync("StockReport");

    private async void ProfitReport_Tapped(object? sender, TappedEventArgs e)
        => await NavigationGuard.GoToAsync("ProfitReport");

    private async void CreditReport_Tapped(object? sender, TappedEventArgs e)
        => await NavigationGuard.GoToAsync("CreditReports");

    private async void ReopenBook_Tapped(object? sender, TappedEventArgs e)
        => await _viewModel.ReopenDailyBookAsync();

    private async void BookLog_Tapped(object? sender, TappedEventArgs e)
        => await _viewModel.NavigateBookLogAsync();

    private async void ImportPartners_Tapped(object? sender, TappedEventArgs e)
        => await _viewModel.NavigateImportAsync("partners");

    private async void ImportInventory_Tapped(object? sender, TappedEventArgs e)
        => await _viewModel.NavigateImportAsync("inventory");

    private async void ImportSales_Tapped(object? sender, TappedEventArgs e)
        => await _viewModel.NavigateImportAsync("sales");

    private async void WholesaleBill_Tapped(object? sender, TappedEventArgs e)
        => await _viewModel.NavigateWholesaleBillAsync();

    private async void Profile_Tapped(object? sender, TappedEventArgs e)
        => await NavigationGuard.GoToAsync("Profile");

    private async void ChangePassword_Tapped(object? sender, TappedEventArgs e)
        => await NavigationGuard.GoToAsync("ChangePassword");

    private async void OnRestoreClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not BackupInfo backup)
            return;

        if (_viewModel.IsRestoring)
            return;

        bool confirmed = await DisplayAlertAsync(
            "Restore Backup",
            $"Restore database from backup '{backup.FileName}'?\n\nAll current data will be replaced.",
            "Restore",
            "Cancel");

        if (!confirmed)
            return;

        bool success = await _viewModel.RestoreAsync(backup);

        if (success)
        {
            await DisplayAlertAsync(
                "Restore Complete",
                "Database restored successfully. The app will now restart.",
                "OK");

            RestartApp();
        }
    }

    private static void RestartApp()
    {
#if WINDOWS
        var process = new System.Diagnostics.ProcessStartInfo
        {
            FileName = Environment.ProcessPath,
            UseShellExecute = true
        };
        System.Diagnostics.Process.Start(process);
#endif
        Application.Current?.Quit();
    }
}