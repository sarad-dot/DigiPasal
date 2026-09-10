using DigiPasal.Services;
using DigiPasal.ViewModels;

namespace DigiPasal.Views;

[QueryProperty(nameof(CustomerId), "customerId")]
[QueryProperty(nameof(Book), "book")]
public partial class CreditDetailPage : ContentPage
{
    private readonly AuthService _authService;
    private readonly CreditDetailViewModel _viewModel;

    public CreditDetailPage(AuthService authService)
    {
        InitializeComponent();
        _authService = authService;
        _viewModel = new CreditDetailViewModel();
        BindingContext = _viewModel;
    }

    public int CustomerId { get; set; }

    public int Book { get; set; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_authService.IsAuthenticated)
        {
            await Shell.Current.GoToAsync("//Login");
            return;
        }

        _viewModel.SetParameters(CustomerId, Book);
        await _viewModel.LoadAsync();
    }
}