using System.Collections.ObjectModel;
using System.Windows.Input;
using DigiPasal.Models;
using DigiPasal.Services;

namespace DigiPasal.ViewModels;

public class SalesHistoryViewModel : BaseViewModel
{
    private readonly SaleService _saleService;

    private ObservableCollection<Sale> _sales = new();
    private DateTime _dateFrom = DateTime.Today.AddDays(-30);
    private DateTime _dateTo = DateTime.Today;
    private string _searchQuery = string.Empty;
    private decimal _totalSales;
    private decimal _cashSales;
    private decimal _creditSales;
    private int _salesCount;
    private readonly Debouncer _searchDebouncer = new();

    public ObservableCollection<Sale> Sales
    {
        get => _sales;
        set => SetProperty(ref _sales, value);
    }

    public DateTime DateFrom
    {
        get => _dateFrom;
        set => SetProperty(ref _dateFrom, value);
    }

    public DateTime DateTo
    {
        get => _dateTo;
        set => SetProperty(ref _dateTo, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
                _searchDebouncer.Debounce(300, LoadSalesAsync);
        }
    }

    public decimal TotalSales
    {
        get => _totalSales;
        set => SetProperty(ref _totalSales, value);
    }

    public decimal CashSales
    {
        get => _cashSales;
        set => SetProperty(ref _cashSales, value);
    }

    public decimal CreditSales
    {
        get => _creditSales;
        set => SetProperty(ref _creditSales, value);
    }

    public int SalesCount
    {
        get => _salesCount;
        set => SetProperty(ref _salesCount, value);
    }

    public string TotalSalesDisplay => CurrencyFormatter.Format(TotalSales);
    public string CashSalesDisplay => CurrencyFormatter.Format(CashSales);
    public string CreditSalesDisplay => CurrencyFormatter.Format(CreditSales);

    public ICommand LoadSalesCommand { get; }
    public ICommand ViewSaleCommand { get; }
    public ICommand SetTodayCommand { get; }
    public ICommand SetWeekCommand { get; }
    public ICommand SetMonthCommand { get; }

    public SalesHistoryViewModel()
    {
        _saleService = SaleService.Instance;
        Title = "Sales History";

        LoadSalesCommand = new Command(async () => await LoadSalesAsync());
        ViewSaleCommand = new Command<Sale>(async (sale) => await ViewSaleAsync(sale));
        SetTodayCommand = new Command(() => { DateFrom = DateTime.Today; DateTo = DateTime.Today; _ = LoadSalesAsync(); });
        SetWeekCommand = new Command(() => { DateFrom = DateTime.Today.AddDays(-7); DateTo = DateTime.Today; _ = LoadSalesAsync(); });
        SetMonthCommand = new Command(() => { DateFrom = DateTime.Today.AddMonths(-1); DateTo = DateTime.Today; _ = LoadSalesAsync(); });
    }

    public async Task LoadSalesAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var sales = await _saleService.GetSalesAsync(DateFrom, DateTo, SearchQuery);
            Sales = new ObservableCollection<Sale>(sales);

            SalesCount = sales.Count;
            TotalSales = sales.Sum(s => s.GrandTotal);
            CashSales = sales.Where(s => !s.IsCredit).Sum(s => s.GrandTotal);
            CreditSales = sales.Where(s => s.IsCredit).Sum(s => s.CreditAmount);

            OnPropertyChanged(nameof(TotalSalesDisplay));
            OnPropertyChanged(nameof(CashSalesDisplay));
            OnPropertyChanged(nameof(CreditSalesDisplay));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ViewSaleAsync(Sale? sale)
    {
        if (sale == null) return;
        await Shell.Current.GoToAsync($"SaleDetail?id={sale.Id}");
    }
}
