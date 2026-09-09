using DigiPasal.Models;
using DigiPasal.Services;
using DigiPasal.ViewModels;

namespace DigiPasal.Views;

public partial class CartPage : ContentPage
{
    private readonly AuthService _authService;
    private readonly CartViewModel _viewModel;

    public CartPage(AuthService authService)
    {
        InitializeComponent();
        _authService = authService;
        _viewModel = new CartViewModel();
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

        await _viewModel.LoadTaxRateAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Cleanup();
    }
}
