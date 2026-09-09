using DigiPasal.ViewModels;

namespace DigiPasal.Views;

public partial class AddEditProductPage : ContentPage
{
    private readonly AddEditProductViewModel _viewModel;

    public AddEditProductPage()
    {
        InitializeComponent();
        _viewModel = new AddEditProductViewModel();
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
    }
}
