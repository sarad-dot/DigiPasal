using DigiPasal.Services;

namespace DigiPasal.Views;

public partial class ProductPage : ContentPage
{
    private readonly AuthService _authService;

    public ProductPage(AuthService authService)
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
