using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using DigiPasal.Services;

namespace DigiPasal.ViewModels;

public class ProfitReportViewModel : BaseViewModel
{
    private readonly SaleService _saleService;

    private DateTime _fromDate = DateTime.Today.AddDays(-30);
    private DateTime _toDate = DateTime.Today;
    private decimal _totalRevenue;
    private decimal _totalCost;
    private int _totalCount;
    private bool _isLoadingDates;
    private bool _isExporting;

    public ObservableCollection<ProfitDayRow> Rows { get; } = new();

    public ProfitReportViewModel()
    {
        _saleService = SaleService.Instance;
        Title = "Profit Report";

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

    public decimal TotalRevenue
    {
        get => _totalRevenue;
        set { SetProperty(ref _totalRevenue, value); OnPropertyChanged(nameof(TotalRevenueDisplay)); }
    }

    public decimal TotalCost
    {
        get => _totalCost;
        set { SetProperty(ref _totalCost, value); OnPropertyChanged(nameof(TotalCostDisplay)); }
    }

    public int TotalCount
    {
        get => _totalCount;
        set { SetProperty(ref _totalCount, value); OnPropertyChanged(nameof(TotalCountDisplay)); }
    }

    public decimal EstimatedProfit => TotalRevenue - TotalCost;

    public string TotalRevenueDisplay => CurrencyFormatter.Format(TotalRevenue);
    public string TotalCostDisplay => CurrencyFormatter.Format(TotalCost);
    public string TotalCountDisplay => $"{TotalCount} sales";

    public string EstimatedProfitDisplay
    {
        get
        {
            var text = CurrencyFormatter.Format(EstimatedProfit);
            return EstimatedProfit < 0 ? text : $" + {text}";
        }
    }

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
            var rows = await _saleService.GetProfitReportAsync(FromDate, ToDate);

            TotalRevenue = rows.Sum(r => r.Revenue);
            TotalCost = rows.Sum(r => r.CostOfGoods);
            TotalCount = rows.Sum(r => r.SalesCount);

            OnPropertyChanged(nameof(EstimatedProfit));
            OnPropertyChanged(nameof(EstimatedProfitDisplay));

            Rows.Clear();
            foreach (var row in rows)
                Rows.Add(row);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to load profit report: {ex.Message}", "OK");
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
                    r.DayKey,
                    CreditReportExportService.FormatN(r.Revenue),
                    CreditReportExportService.FormatN(r.CostOfGoods),
                    CreditReportExportService.FormatN(r.Revenue - r.CostOfGoods),
                    r.SalesCount.ToString()
                });

            if (rows.Count == 0)
            {
                await Shell.Current.DisplayAlertAsync("Export", "No profit data to export.", "OK");
                return;
            }

            await CreditReportExportService.ExportAsync(
                "Profit Report (Estimated)",
                $"profit_report_{FromDate:yyyyMMdd}_{ToDate:yyyyMMdd}.csv",
                new[] { "Date", "Revenue", "Cost of Goods", "Estimated Profit", "Sales" },
                rows);
        }
        finally
        {
            IsExporting = false;
        }
    }
}