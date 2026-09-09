using DigiPasal.ViewModels;

namespace DigiPasal.Views;

public partial class SaleDetailPage : ContentPage
{
    private readonly SaleDetailViewModel _viewModel;

    public SaleDetailPage()
    {
        InitializeComponent();
        _viewModel = new SaleDetailViewModel();
        BindingContext = _viewModel;
    }
}
