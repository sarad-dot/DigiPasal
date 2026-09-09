using DigiPasal.ViewModels;

namespace DigiPasal.Views;

public partial class SaleSuccessPage : ContentPage
{
    private readonly SaleSuccessViewModel _viewModel;

    public SaleSuccessPage()
    {
        InitializeComponent();
        _viewModel = new SaleSuccessViewModel();
        BindingContext = _viewModel;
    }
}
