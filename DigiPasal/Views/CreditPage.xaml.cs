using DigiPasal.Services;

namespace DigiPasal.Views;

public partial class CreditPage : ContentPage
{
    private readonly AuthService _authService;

    public CreditPage(AuthService authService)
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
