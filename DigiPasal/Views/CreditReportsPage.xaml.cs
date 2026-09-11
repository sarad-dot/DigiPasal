using DigiPasal.Services;
using DigiPasal.ViewModels;

namespace DigiPasal.Views;

public partial class CreditReportsPage : ContentPage
{
    private readonly AuthService _authService;
    private readonly CreditReportsViewModel _viewModel;

    public CreditReportsPage(AuthService authService)
    {
        InitializeComponent();
        _authService = authService;
        _viewModel = new CreditReportsViewModel();
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

        await _viewModel.LoadAsync();
    }

    private async void AgingRow_Tapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not CreditAgingRow row)
            return;

        await NavigationGuard.GoToAsync($"CreditDetail?customerId={row.CustomerId}&book=0");
    }

    private async void PaymentsRow_Tapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not PaymentHistoryRow row)
            return;

        await NavigationGuard.GoToAsync($"CreditDetail?customerId={row.CustomerId}&book=0");
    }
}
