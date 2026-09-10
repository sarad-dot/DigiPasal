using DigiPasal.ViewModels;

namespace DigiPasal.Views;

public partial class ForgetPasswordPage : ContentPage
{
    public ForgetPasswordPage(ForgotPasswordViewModel viewModel)
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