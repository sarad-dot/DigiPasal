using DigiPasal.Services;
using DigiPasal.ViewModels;

namespace DigiPasal.Views;

public partial class WholesaleBillPage : ContentPage
{
    private readonly AuthService _authService;
    private readonly WholesaleBillViewModel _viewModel;

    public WholesaleBillPage(WholesaleBillViewModel viewModel, AuthService authService)
    {
        InitializeComponent();
        _authService = authService;
        _viewModel = viewModel;
        BindingContext = viewModel;
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

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnAddTapped(object? sender, TappedEventArgs e)
    {
        await _viewModel.NavigateToNewAsync();
    }

    private async void OnBillTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is Services.PurchaseSummaryItem item)
            await _viewModel.OpenAsync(item);
    }
}