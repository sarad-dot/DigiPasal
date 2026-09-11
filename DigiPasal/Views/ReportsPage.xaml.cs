using DigiPasal.Services;

namespace DigiPasal.Views;

public partial class ReportsPage : ContentPage
{
    private readonly AuthService _authService;

    public ReportsPage(AuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_authService.IsAuthenticated)
        {
            Shell.Current.GoToAsync("//Login");
        }
    }

    private async void SalesReport_Tapped(object? sender, TappedEventArgs e)
        => await NavigationGuard.GoToAsync("SalesReport");

    private async void StockReport_Tapped(object? sender, TappedEventArgs e)
        => await NavigationGuard.GoToAsync("StockReport");

    private async void ProfitReport_Tapped(object? sender, TappedEventArgs e)
        => await NavigationGuard.GoToAsync("ProfitReport");

    private async void CreditReport_Tapped(object? sender, TappedEventArgs e)
        => await NavigationGuard.GoToAsync("CreditReports");
}