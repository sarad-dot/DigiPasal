using System;
using DigiPasal.Services;
using DigiPasal.ViewModels;
using Microsoft.Maui.Controls;

namespace DigiPasal.Views;

public partial class DashboardPage : ContentPage
{
    private readonly AuthService _authService;
    private readonly DashboardViewModel _viewModel;

    public DashboardPage(AuthService authService)
    {
        InitializeComponent();
        _authService = authService;
        _viewModel = new DashboardViewModel(authService);
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_authService.IsAuthenticated)
        {
            await Shell.Current.GoToAsync("//Login");
            return;
        }

        await LoadUserAsync();
        await _viewModel.LoadUserAsync();
    }

    private async Task LoadUserAsync()
    {
        var user = await _authService.GetCurrentUserAsync();
        if (user == null)
            return;

        string first = string.IsNullOrWhiteSpace(user.FullName)
            ? "Profile"
            : user.FullName.Trim().Split(' ')[0];

        ProfileNameLabel.Text = first;
        ProfileAvatarLabel.Text = BuildInitials(user.FullName);

        MenuUserNameLabel.Text = string.IsNullOrWhiteSpace(user.FullName)
            ? user.Username
            : user.FullName;
        MenuUserHandleLabel.Text = "@" + user.Username;
    }

    private static string BuildInitials(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return "👤";

        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return parts[0][..1].ToUpperInvariant();

        return (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
    }

#if ANDROID
    protected override bool OnBackButtonPressed()
    {
        if (ProfileDropdownOverlay.IsVisible)
        {
            ProfileDropdownOverlay.IsVisible = false;
            return true;
        }

        _ = HandleBackPressedAsync();
        return true;
    }

    private async Task HandleBackPressedAsync()
    {
        string action = await DisplayActionSheetAsync(
            "Leave DigiPasal?",
            "Cancel",
            null,
            "Logout",
            "Exit App");

        if (action == "Logout")
        {
            _authService.SignOut();
            await Shell.Current.GoToAsync("//Login");
        }
        else if (action == "Exit App")
        {
            Application.Current?.Quit();
        }
    }
#endif

    // ============================================================
    // HEADER - PROFILE DROPDOWN
    // ============================================================

    private void ProfilePill_Tapped(
        object? sender,
        TappedEventArgs e)
    {
        ProfileDropdownOverlay.IsVisible = !ProfileDropdownOverlay.IsVisible;
    }

    private void ProfileDropdownDismiss_Tapped(
        object? sender,
        TappedEventArgs e)
    {
        ProfileDropdownOverlay.IsVisible = false;
    }

    private async void MenuProfile_Tapped(
        object? sender,
        TappedEventArgs e)
    {
        ProfileDropdownOverlay.IsVisible = false;
        await Shell.Current.GoToAsync("Profile");
    }

    private async void MenuChangePassword_Tapped(
        object? sender,
        TappedEventArgs e)
    {
        ProfileDropdownOverlay.IsVisible = false;
        await Shell.Current.GoToAsync("ChangePassword");
    }

    private async void MenuLogout_Tapped(
        object? sender,
        TappedEventArgs e)
    {
        ProfileDropdownOverlay.IsVisible = false;
        _authService.SignOut();
        await Shell.Current.GoToAsync("//Login");
    }


    // ============================================================
    // EXPENSES
    // ============================================================

    private async void ExpensesDetails_Clicked(
        object? sender,
        EventArgs e)
    {
        await DisplayAlertAsync(
            "Expenses",
            "Expenses details screen coming soon.",
            "OK");
    }


    // ============================================================
    // QUICK ACTIONS
    // ============================================================

    private async void SaleAction_Tapped(
        object? sender,
        TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("Sales");
    }


    private async void SalesReport_Tapped(
        object? sender,
        TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("SalesReport");
    }


    private async void StockReport_Tapped(
        object? sender,
        TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("StockReport");
    }


    private async void ProfitReport_Tapped(
        object? sender,
        TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("ProfitReport");
    }


    private async void CreditReport_Tapped(
        object? sender,
        TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("CreditReports");
    }


    private async void Reports_Tapped(
        object? sender,
        TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("Reports");
    }


    private async void QuickSaleAction_Tapped(
        object? sender,
        TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("QuickSale");
    }


    // ============================================================
    // BOTTOM NAVIGATION
    // ============================================================

    private void HomeTab_Tapped(
        object? sender,
        TappedEventArgs e)
    {
        // Already on Dashboard/Home.
    }


    private async void SalesTab_Tapped(
        object? sender,
        TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("Sales");
    }


    private async void CreditTab_Tapped(
        object? sender,
        TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("Credit");
    }


    private async void StockTab_Tapped(
        object? sender,
        TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("Products");
    }


    private async void SettingsTab_Tapped(
        object? sender,
        TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("Settings");
    }
}