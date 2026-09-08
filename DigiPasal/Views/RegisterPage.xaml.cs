using DigiPasal.ViewModels;

namespace DigiPasal.Views;

public partial class RegisterPage : ContentPage
{
    public RegisterPage(RegisterViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override bool OnBackButtonPressed()
    {
        _ = Shell.Current.GoToAsync("//Login");
        return true;
    }
}