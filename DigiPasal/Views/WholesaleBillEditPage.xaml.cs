using DigiPasal.Services;
using DigiPasal.ViewModels;

namespace DigiPasal.Views;

public partial class WholesaleBillEditPage : ContentPage
{
    private readonly AuthService _authService;
    private readonly WholesaleBillEditViewModel _viewModel;

    public WholesaleBillEditPage(WholesaleBillEditViewModel viewModel, AuthService authService)
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

    private void OnRemoveLineTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is BillItemEdit item)
            _viewModel.RemoveItemCommand.Execute(item);
    }
}