using DigiPasal.Services;
using DigiPasal.ViewModels;

namespace DigiPasal.Views;

public partial class CreditPage : ContentPage
{
    private readonly AuthService _authService;
    private readonly CreditViewModel _viewModel;

    public CreditPage(AuthService authService)
    {
        InitializeComponent();
        _authService = authService;
        _viewModel = new CreditViewModel();
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

    private async void DailyEntry_Tapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not DailyBookEntry entry)
            return;

        await _viewModel.NavigateToCustomerAsync(entry, null);
    }

    private async void PartnerItem_Tapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not PartnerCreditItem item)
            return;

        await _viewModel.NavigateToCustomerAsync(null, item);
    }
}