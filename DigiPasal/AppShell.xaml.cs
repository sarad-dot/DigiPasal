namespace DigiPasal;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("ForgotPassword", typeof(Views.ForgetPasswordPage));
        Routing.RegisterRoute("Sales", typeof(Views.SalePage));
        Routing.RegisterRoute("Products", typeof(Views.ProductPage));
        Routing.RegisterRoute("Credit", typeof(Views.CreditPage));
        Routing.RegisterRoute("Profile", typeof(Views.ProfilePage));
        Routing.RegisterRoute("ChangePassword", typeof(Views.ChangePasswordPage));
    }
}