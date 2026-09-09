using DigiPasal.Services;
using DigiPasal.ViewModels;

namespace DigiPasal.Views;

public partial class QuickSalePage : ContentPage
{
    private readonly AuthService _authService;
    private readonly QuickSaleViewModel _viewModel;

    public QuickSalePage(AuthService authService)
    {
        InitializeComponent();
        _authService = authService;
        _viewModel = new QuickSaleViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_authService.IsAuthenticated)
        {
            await Shell.Current.GoToAsync("//Login");
        }
    }
}
