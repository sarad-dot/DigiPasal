using DigiPasal.Services;

namespace DigiPasal.Views;

public partial class SalePage : ContentPage
{
    private readonly AuthService _authService;

    public SalePage(AuthService authService)
    {
        InitializeComponent();
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
