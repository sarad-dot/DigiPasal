using DigiPasal.Services;
using DigiPasal.ViewModels;

namespace DigiPasal.Views;

public partial class ChangePasswordPage : ContentPage
{
    private readonly AuthService _authService;

    public ChangePasswordPage(ChangePasswordViewModel viewModel, AuthService authService)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _authService = authService;
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