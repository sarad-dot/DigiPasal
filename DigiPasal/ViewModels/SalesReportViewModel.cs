using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using DigiPasal.Services;

namespace DigiPasal.ViewModels;

public class SalesReportViewModel : BaseViewModel
{
    private readonly SaleService _saleService;

    private DateTime _fromDate = DateTime.Today.AddDays(-30);
    private DateTime _toDate = DateTime.Today;
    private decimal _totalSales;
    private decimal _totalCash;
    private decimal _totalCredit;
    private int _totalCount;
    private int _totalItems;
    private bool _isLoadingDates;
    private bool _isExporting;

    public ObservableCollection<DailySalesSummary> Rows { get; } = new();

    public SalesReportViewModel()
    {
        _saleService = SaleService.Instance;
        Title = "Sales Report";

        TodayCommand = new Command(() => { FromDate = DateTime.Today; ToDate = DateTime.Today; });
        WeekCommand = new Command(() => { ToDate = DateTime.Today; FromDate = DateTime.Today.AddDays(-6); });
        MonthCommand = new Command(() => { ToDate = DateTime.Today; FromDate = DateTime.Today.AddDays(-29); });
        ExportCommand = new Command(async () => await ExportAsync());

        PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoadingDates)
            return;

        if (e.PropertyName is nameof(FromDate) or nameof(ToDate))
        {
            if (FromDate > ToDate)
                return;
            _ = LoadAsync();
        }
    }

    public DateTime FromDate
    {
        get => _fromDate;
        set => SetProperty(ref _fromDate, value);
    }

    public DateTime ToDate
    {
        get => _toDate;
        set => SetProperty(ref _toDate, value);
    }

    public decimal TotalSales
    {
        get => _totalSales;
        set { SetProperty(ref _totalSales, value); OnPropertyChanged(nameof(TotalSalesDisplay)); }
    }

    public decimal TotalCash
    {
        get => _totalCash;
        set { SetProperty(ref _totalCash, value); OnPropertyChanged(nameof(TotalCashDisplay)); }
    }

    public decimal TotalCredit
    {
        get => _totalCredit;
        set { SetProperty(ref _totalCredit, value); OnPropertyChanged(nameof(TotalCreditDisplay)); }
    }

    public int TotalCount
    {
        get => _totalCount;
        set { SetProperty(ref _totalCount, value); OnPropertyChanged(nameof(TotalCountDisplay)); }
    }

    public int TotalItems
    {
        get => _totalItems;
        set { SetProperty(ref _totalItems, value); OnPropertyChanged(nameof(TotalItemsDisplay)); }
    }

    public string TotalSalesDisplay => CurrencyFormatter.Format(TotalSales);
    public string TotalCashDisplay => CurrencyFormatter.Format(TotalCash);
    public string TotalCreditDisplay => CurrencyFormatter.Format(TotalCredit);
    public string TotalCountDisplay => $"{TotalCount} sales";
    public string TotalItemsDisplay => $"{TotalItems} items";

    public bool IsExporting
    {
        get => _isExporting;
        set => SetProperty(ref _isExporting, value);
    }

    public ICommand TodayCommand { get; }
    public ICommand WeekCommand { get; }
    public ICommand MonthCommand { get; }
    public ICommand ExportCommand { get; }

    public async Task LoadAsync()
    {
        IsBusy = true;
        _isLoadingDates = true;
        try
        {
            var rows = await _saleService.GetDailySalesReportAsync(FromDate, ToDate);

            TotalSales = rows.Sum(r => r.TotalSales);
            TotalCash = rows.Sum(r => r.CashSales);
            TotalCredit = rows.Sum(r => r.CreditSales);
            TotalCount = rows.Sum(r => r.SalesCount);
            TotalItems = rows.Sum(r => r.ItemsSold);

            Rows.Clear();
            foreach (var row in rows)
                Rows.Add(row);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to load sales report: {ex.Message}", "OK");
        }
        finally
        {
            _isLoadingDates = false;
            IsBusy = false;
        }
    }

    private async Task ExportAsync()
    {
        if (IsExporting)
            return;

        IsExporting = true;
        try
        {
            var rows = new List<string[]>(Rows.Count);
            foreach (var r in Rows)
                rows.Add(new[]
                {
                    r.Date.ToString("yyyy-MM-dd"),
                    CreditReportExportService.FormatN(r.TotalSales),
                    CreditReportExportService.FormatN(r.CashSales),
                    CreditReportExportService.FormatN(r.CreditSales),
                    r.SalesCount.ToString(),
                    r.ItemsSold.ToString()
                });

            if (rows.Count == 0)
            {
                await Shell.Current.DisplayAlertAsync("Export", "No sales data to export.", "OK");
                return;
            }

            await CreditReportExportService.ExportAsync(
                "Sales Report",
                $"sales_report_{FromDate:yyyyMMdd}_{ToDate:yyyyMMdd}.csv",
                new[] { "Date", "Total", "Cash", "Credit", "Sales", "Items" },
                rows);
        }
        finally
        {
            IsExporting = false;
        }
    }
}