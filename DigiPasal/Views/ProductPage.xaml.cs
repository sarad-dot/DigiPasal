using DigiPasal.Services;
using DigiPasal.ViewModels;

namespace DigiPasal.Views;

public partial class ProductPage : ContentPage
{
    private readonly AuthService _authService;
    private readonly ProductListViewModel _viewModel;

    public ProductPage(AuthService authService)
    {
        InitializeComponent();
        _authService = authService;
        _viewModel = new ProductListViewModel();
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
