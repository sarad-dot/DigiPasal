using DigiPasal.Services;
using DigiPasal.ViewModels;

namespace DigiPasal.Views;

public partial class SalesHistoryPage : ContentPage
{
    private readonly AuthService _authService;
    private readonly SalesHistoryViewModel _viewModel;

    public SalesHistoryPage(AuthService authService)
    {
        InitializeComponent();
        _authService = authService;
        _viewModel = new SalesHistoryViewModel();
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

        await _viewModel.LoadSalesAsync();
    }
}
