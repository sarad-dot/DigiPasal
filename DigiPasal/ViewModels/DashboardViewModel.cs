using System.Windows.Input;
using DigiPasal.Models;
using DigiPasal.Services;

namespace DigiPasal.ViewModels
{
    public class DashboardViewModel : BaseViewModel
    {
        private readonly AuthService _authService;

        private string _fullName = string.Empty;
        private string _shopName = string.Empty;
        private string _greeting = string.Empty;
        private decimal _todaySalesAmount;
        private int _todaySalesCount;
        private int _lowStockCount;
        private decimal _pendingCreditAmount;

        public string FullName
        {
            get => _fullName;
            set => SetProperty(ref _fullName, value);
        }

        public string ShopName
        {
            get => _shopName;
            set => SetProperty(ref _shopName, value);
        }

        public string Greeting
        {
            get => _greeting;
            set => SetProperty(ref _greeting, value);
        }

        public decimal TodaySalesAmount
        {
            get => _todaySalesAmount;
            set
            {
                SetProperty(ref _todaySalesAmount, value);
                OnPropertyChanged(nameof(TodaySalesDisplay));
            }
        }

        public int TodaySalesCount
        {
            get => _todaySalesCount;
            set
            {
                SetProperty(ref _todaySalesCount, value);
                OnPropertyChanged(nameof(TodaySalesCountDisplay));
            }
        }

        public int LowStockCount
        {
            get => _lowStockCount;
            set => SetProperty(ref _lowStockCount, value);
        }

        public decimal PendingCreditAmount
        {
            get => _pendingCreditAmount;
            set
            {
                SetProperty(ref _pendingCreditAmount, value);
                OnPropertyChanged(nameof(PendingCreditDisplay));
            }
        }

        public string TodaySalesDisplay => CurrencyFormatter.Format(TodaySalesAmount);
        public string TodaySalesCountDisplay => $"{TodaySalesCount} sales today";
        public string PendingCreditDisplay => CurrencyFormatter.Format(PendingCreditAmount);

        public ICommand NavigateToSalesCommand { get; }
        public ICommand NavigateToProductsCommand { get; }
        public ICommand NavigateToCreditCommand { get; }
        public ICommand NavigateToNewSaleCommand { get; }
        public ICommand NavigateToSalesHistoryCommand { get; }
        public ICommand LogoutCommand { get; }

        public DashboardViewModel(AuthService authService)
        {
            _authService = authService;
            Title = "Dashboard";

            NavigateToSalesCommand = new Command(async () => await Shell.Current.GoToAsync("Sales"));
            NavigateToProductsCommand = new Command(async () => await Shell.Current.GoToAsync("Products"));
            NavigateToCreditCommand = new Command(async () => await Shell.Current.GoToAsync("Credit"));
            NavigateToNewSaleCommand = new Command(async () => await Shell.Current.GoToAsync("Sales"));
            NavigateToSalesHistoryCommand = new Command(async () => await Shell.Current.GoToAsync("SalesHistory"));
            LogoutCommand = new Command(async () => await Logout());
        }

        public async Task LoadUserAsync()
        {
            IsBusy = true;
            try
            {
                User? user = await _authService.GetCurrentUserAsync();
                if (user != null)
                {
                    FullName = user.FullName;
                    ShopName = user.ShopName;
                    Greeting = $"Welcome, {user.FullName}";
                }

                await LoadDashboardStatsAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadDashboardStatsAsync()
        {
            try
            {
                var summary = await SaleService.Instance.GetDailySalesSummaryAsync(DateTime.Today);
                TodaySalesAmount = summary.TotalSales;
                TodaySalesCount = summary.SalesCount;

                var lowStock = await ProductService.Instance.GetLowStockProductsAsync();
                LowStockCount = lowStock.Count;

                var creditCustomers = await CustomerService.Instance.GetCustomersWithBalanceAsync();
                PendingCreditAmount = creditCustomers.Sum(c => c.CurrentBalance);
            }
            catch
            {
                // Stats are non-critical, fail silently
            }
        }

        private async Task Logout()
        {
            _authService.SignOut();
            await Shell.Current.GoToAsync("//Login");
        }
    }
}