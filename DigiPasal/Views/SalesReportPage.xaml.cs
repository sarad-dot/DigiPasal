using DigiPasal.Services;
using DigiPasal.ViewModels;

namespace DigiPasal.Views;

public partial class SalesReportPage : ContentPage
{
    private readonly AuthService _authService;
    private readonly SalesReportViewModel _viewModel;

    public SalesReportPage(AuthService authService)
    {
        InitializeComponent();
        _authService = authService;
        _viewModel = new SalesReportViewModel();
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
}