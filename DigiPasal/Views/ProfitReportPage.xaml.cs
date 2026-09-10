using DigiPasal.Services;
using DigiPasal.ViewModels;

namespace DigiPasal.Views;

public partial class ProfitReportPage : ContentPage
{
    private readonly AuthService _authService;
    private readonly ProfitReportViewModel _viewModel;

    public ProfitReportPage(AuthService authService)
    {
        InitializeComponent();
        _authService = authService;
        _viewModel = new ProfitReportViewModel();
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