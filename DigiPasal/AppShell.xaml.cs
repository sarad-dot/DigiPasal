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
        Routing.RegisterRoute("CreditDetail", typeof(Views.CreditDetailPage));
        Routing.RegisterRoute("CreditReports", typeof(Views.CreditReportsPage));
        Routing.RegisterRoute("RecordPayment", typeof(Views.RecordPaymentPage));
        Routing.RegisterRoute("Profile", typeof(Views.ProfilePage));
        Routing.RegisterRoute("ChangePassword", typeof(Views.ChangePasswordPage));
        Routing.RegisterRoute("Settings", typeof(Views.SettingsPage));
        Routing.RegisterRoute("QuickSale", typeof(Views.QuickSalePage));
        Routing.RegisterRoute("Reports", typeof(Views.ReportsPage));
        Routing.RegisterRoute("SalesReport", typeof(Views.SalesReportPage));
        Routing.RegisterRoute("StockReport", typeof(Views.StockReportPage));
        Routing.RegisterRoute("ProfitReport", typeof(Views.ProfitReportPage));
        Routing.RegisterRoute("ShopSettings", typeof(Views.ShopSettingsPage));
    }
}