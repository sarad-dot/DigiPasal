using DigiPasal.Services;
using DigiPasal.ViewModels;

namespace DigiPasal.Views;

public partial class DayBookLogPage : ContentPage
{
    private readonly AuthService _authService;
    private readonly DayBookLogViewModel _viewModel;

    public DayBookLogPage(DayBookLogViewModel viewModel, AuthService authService)
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
}