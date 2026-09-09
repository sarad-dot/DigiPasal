using DigiPasal.Services;
using DigiPasal.ViewModels;

namespace DigiPasal.Views;

public partial class SalePage : ContentPage
{
    private readonly AuthService _authService;
    private readonly SaleViewModel _viewModel;

    public SalePage(AuthService authService)
    {
        InitializeComponent();
        _authService = authService;
        _viewModel = new SaleViewModel();
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

        await _viewModel.LoadProductsAsync();
    }
}
