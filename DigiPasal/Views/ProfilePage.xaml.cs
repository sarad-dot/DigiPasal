using DigiPasal.Services;
using DigiPasal.ViewModels;

namespace DigiPasal.Views;

public partial class ProfilePage : ContentPage
{
    private readonly AuthService _authService;
    private readonly ProfileViewModel _viewModel;

    public ProfilePage(ProfileViewModel viewModel, AuthService authService)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
        _authService = authService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_authService.IsAuthenticated)
        {
            await Shell.Current.GoToAsync("//Login");
            return;
        }

        await _viewModel.LoadUserAsync();
    }
}