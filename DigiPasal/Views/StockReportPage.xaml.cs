using DigiPasal.Services;
using DigiPasal.ViewModels;

namespace DigiPasal.Views;

public partial class StockReportPage : ContentPage
{
    private readonly AuthService _authService;
    private readonly StockReportViewModel _viewModel;

    public StockReportPage(AuthService authService)
    {
        InitializeComponent();
        _authService = authService;
        _viewModel = new StockReportViewModel();
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
        ResyncPickerIndex();
    }

    private void CategoryPicker_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (sender is Picker picker && picker.SelectedIndex >= 0 && picker.SelectedIndex < picker.ItemsSource.Count)
        {
            var selected = picker.ItemsSource[picker.SelectedIndex] as string;
            if (selected != null && selected != _viewModel.SelectedCategory)
            {
                _viewModel.SelectedCategory = selected;
                _ = LoadAndResyncAsync();
            }
        }
    }

    private async Task LoadAndResyncAsync()
    {
        await _viewModel.LoadAsync();
        ResyncPickerIndex();
    }

    private void ResyncPickerIndex()
    {
        if (CategoryPicker != null)
        {
            var index = _viewModel.Categories.IndexOf(_viewModel.SelectedCategory);
            CategoryPicker.SelectedIndex = index;
        }
    }
}