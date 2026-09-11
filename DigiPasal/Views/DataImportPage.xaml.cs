using DigiPasal.Services;
using DigiPasal.ViewModels;

namespace DigiPasal.Views;

public partial class DataImportPage : ContentPage
{
    private readonly AuthService _authService;
    private readonly DataImportViewModel _viewModel;

    public DataImportPage(DataImportViewModel viewModel, AuthService authService)
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
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}