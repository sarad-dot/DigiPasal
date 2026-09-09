namespace DigiPasal;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("ForgotPassword", typeof(Views.ForgetPasswordPage));
        Routing.RegisterRoute("Sales", typeof(Views.SalePage));
        Routing.RegisterRoute("Cart", typeof(Views.CartPage));
        Routing.RegisterRoute("SaleSuccess", typeof(Views.SaleSuccessPage));
        Routing.RegisterRoute("SalesHistory", typeof(Views.SalesHistoryPage));
        Routing.RegisterRoute("SaleDetail", typeof(Views.SaleDetailPage));
        Routing.RegisterRoute("Products", typeof(Views.ProductPage));
        Routing.RegisterRoute("AddProduct", typeof(Views.AddEditProductPage));
        Routing.RegisterRoute("EditProduct", typeof(Views.AddEditProductPage));
        Routing.RegisterRoute("Credit", typeof(Views.CreditPage));
        Routing.RegisterRoute("Profile", typeof(Views.ProfilePage));
        Routing.RegisterRoute("ChangePassword", typeof(Views.ChangePasswordPage));
        Routing.RegisterRoute("Settings", typeof(Views.SettingsPage));
        Routing.RegisterRoute("QuickSale", typeof(Views.QuickSalePage));
    }
}