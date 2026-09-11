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
        Routing.RegisterRoute("DayVoucher", typeof(Views.DayVoucherPage));
        Routing.RegisterRoute("DayBookLog", typeof(Views.DayBookLogPage));
        Routing.RegisterRoute("DataImport", typeof(Views.DataImportPage));
        Routing.RegisterRoute("WholesaleBill", typeof(Views.WholesaleBillPage));
        Routing.RegisterRoute("WholesaleBillEdit", typeof(Views.WholesaleBillEditPage));

        Navigating += OnShellNavigating;
    }

    /// <summary>
    /// Safety net: cancels a push whose destination is the same route already on
    /// top of the navigation stack. Absolute top-level navigation and pops are
    /// unaffected. Prevents stacks like Dashboard → Sales → Sales from repeated taps.
    /// </summary>
    private void OnShellNavigating(object? sender, ShellNavigatingEventArgs e)
    {
        if (e.Source != ShellNavigationSource.Push)
            return;

        var current = CurrentState.Location;
        var target = e.Target.Location;
        if (current == null || target == null)
            return;

        if (LastSegment(target.OriginalString) is { } targetTop &&
            !string.IsNullOrEmpty(targetTop) &&
            string.Equals(LastSegment(current.OriginalString), targetTop, StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel();
        }
    }

    private static string? LastSegment(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return null;

        var segment = uri.Split('?')[0].Split('/').LastOrDefault();
        return string.IsNullOrWhiteSpace(segment) ? null : segment;
    }
}