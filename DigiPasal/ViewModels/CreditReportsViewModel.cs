using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using DigiPasal.Services;

namespace DigiPasal.ViewModels;

public class CreditReportsViewModel : BaseViewModel
{
    private readonly CreditService _creditService;

    private List<CreditAgingRow>? _cachedAging;

    private int _selectedTabIndex;
    private DateTime _summaryFromDate = DateTime.Today.AddDays(-30);
    private DateTime _summaryToDate = DateTime.Today;
    private DateTime _historyFromDate = DateTime.Today.AddDays(-30);
    private DateTime _historyToDate = DateTime.Today;
    private bool _isLoadingDates;
    private decimal _totalOutstanding;
    private decimal _totalDailyOutstanding;
    private decimal _totalPartnerOutstanding;
    private int _criticalCount;
    private int _totalCustomersWithCredit;
    private decimal _totalPaymentsReceived;
    private decimal _totalNewCredit;
    private decimal _summaryNewCredit;
    private decimal _summaryPayments;
    private bool _isExporting;

    public ObservableCollection<DailyCreditSummaryRow> SummaryRows { get; } = new();
    public ObservableCollection<CreditAgingRow> AgingRows { get; } = new();
    public ObservableCollection<PaymentHistoryRow> PaymentRows { get; } = new();
    public ObservableCollection<CreditTrendPoint> TrendPoints { get; } = new();

    public CreditReportsViewModel()
    {
        _creditService = CreditService.Instance;
        Title = "Credit Reports";

        SwitchTabCommand = new Command<int>(async (idx) => await SwitchTabAsync(idx));
        ExportCommand = new Command(async () => await ExportCurrentTabAsync());

        PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoadingDates)
            return;

        switch (e.PropertyName)
        {
            case nameof(SummaryFromDate):
            case nameof(SummaryToDate):
                if (SummaryFromDate > SummaryToDate)
                    return;
                _ = LoadSummaryAsync();
                break;
            case nameof(HistoryFromDate):
            case nameof(HistoryToDate):
                if (HistoryFromDate > HistoryToDate)
                    return;
                _ = LoadPaymentsAsync();
                break;
        }
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            SetProperty(ref _selectedTabIndex, value);
            OnPropertyChanged(nameof(IsSummaryTab));
            OnPropertyChanged(nameof(IsAgingTab));
            OnPropertyChanged(nameof(IsPaymentsTab));
            OnPropertyChanged(nameof(IsTrendTab));
        }
    }

    public bool IsSummaryTab => SelectedTabIndex == 0;
    public bool IsAgingTab => SelectedTabIndex == 1;
    public bool IsPaymentsTab => SelectedTabIndex == 2;
    public bool IsTrendTab => SelectedTabIndex == 3;

    public DateTime SummaryFromDate
    {
        get => _summaryFromDate;
        set => SetProperty(ref _summaryFromDate, value);
    }

    public DateTime SummaryToDate
    {
        get => _summaryToDate;
        set => SetProperty(ref _summaryToDate, value);
    }

    public DateTime HistoryFromDate
    {
        get => _historyFromDate;
        set => SetProperty(ref _historyFromDate, value);
    }

    public DateTime HistoryToDate
    {
        get => _historyToDate;
        set => SetProperty(ref _historyToDate, value);
    }

    public decimal TotalOutstanding
    {
        get => _totalOutstanding;
        set { SetProperty(ref _totalOutstanding, value); OnPropertyChanged(nameof(TotalOutstandingDisplay)); }
    }

    public decimal TotalDailyOutstanding
    {
        get => _totalDailyOutstanding;
        set { SetProperty(ref _totalDailyOutstanding, value); OnPropertyChanged(nameof(TotalDailyOutstandingDisplay)); }
    }

    public decimal TotalPartnerOutstanding
    {
        get => _totalPartnerOutstanding;
        set { SetProperty(ref _totalPartnerOutstanding, value); OnPropertyChanged(nameof(TotalPartnerOutstandingDisplay)); }
    }

    public int CriticalCount
    {
        get => _criticalCount;
        set { SetProperty(ref _criticalCount, value); OnPropertyChanged(nameof(CriticalCountDisplay)); }
    }

    public int TotalCustomersWithCredit
    {
        get => _totalCustomersWithCredit;
        set { SetProperty(ref _totalCustomersWithCredit, value); OnPropertyChanged(nameof(TotalCustomersDisplay)); }
    }

    public decimal TotalPaymentsReceived
    {
        get => _totalPaymentsReceived;
        set { SetProperty(ref _totalPaymentsReceived, value); OnPropertyChanged(nameof(TotalPaymentsDisplay)); }
    }

    public decimal TotalNewCredit
    {
        get => _totalNewCredit;
        set { SetProperty(ref _totalNewCredit, value); OnPropertyChanged(nameof(TotalNewCreditDisplay)); }
    }

    public decimal SummaryNewCredit
    {
        get => _summaryNewCredit;
        set { SetProperty(ref _summaryNewCredit, value); OnPropertyChanged(nameof(SummaryNewCreditDisplay)); }
    }

    public decimal SummaryPayments
    {
        get => _summaryPayments;
        set { SetProperty(ref _summaryPayments, value); OnPropertyChanged(nameof(SummaryPaymentsDisplay)); }
    }

    public string TotalOutstandingDisplay => CurrencyFormatter.Format(TotalOutstanding);
    public string TotalDailyOutstandingDisplay => CurrencyFormatter.Format(TotalDailyOutstanding);
    public string TotalPartnerOutstandingDisplay => CurrencyFormatter.Format(TotalPartnerOutstanding);
    public string CriticalCountDisplay => CriticalCount > 0 ? $"{CriticalCount} critical (90+ days)" : string.Empty;
    public string TotalCustomersDisplay => $"{TotalCustomersWithCredit} customers";
    public string TotalPaymentsDisplay => CurrencyFormatter.Format(TotalPaymentsReceived);
    public string TotalNewCreditDisplay => CurrencyFormatter.Format(TotalNewCredit);
    public string SummaryNewCreditDisplay => CurrencyFormatter.Format(SummaryNewCredit);
    public string SummaryPaymentsDisplay => CurrencyFormatter.Format(SummaryPayments);

    public bool IsExporting
    {
        get => _isExporting;
        set => SetProperty(ref _isExporting, value);
    }

    public ICommand SwitchTabCommand { get; }
    public ICommand ExportCommand { get; }

    public async Task LoadAsync()
    {
        IsBusy = true;
        _isLoadingDates = true;
        _cachedAging = null;
        try
        {
            await LoadOverviewAsync();
            await LoadCurrentTabAsync();
        }
        finally
        {
            _isLoadingDates = false;
            IsBusy = false;
        }
    }

    private async Task LoadOverviewAsync()
    {
        try
        {
            if (_cachedAging == null)
                _cachedAging = await _creditService.GetCreditAgingAsync();

            var aging = _cachedAging;
            TotalOutstanding = aging.Sum(r => r.TotalBalance);
            TotalDailyOutstanding = aging.Sum(r => r.DailyBalance);
            TotalPartnerOutstanding = aging.Sum(r => r.PartnerBalance);
            CriticalCount = aging.Count(r => r.IsCritical);
            TotalCustomersWithCredit = aging.Count;

            TotalPaymentsReceived = await _creditService.GetTotalPaymentsCollectedAsync();
            TotalNewCredit = await _creditService.GetCreditIssuedTotalAsync(DateTime.Today.AddYears(-1));
        }
        catch
        {
            // Non-critical, fail silently
        }
    }

    private async Task LoadCurrentTabAsync()
    {
        switch (SelectedTabIndex)
        {
            case 0: await LoadSummaryAsync(); break;
            case 1: await LoadAgingAsync(); break;
            case 2: await LoadPaymentsAsync(); break;
            case 3: await LoadTrendAsync(); break;
        }
    }

    private async Task SwitchTabAsync(int index)
    {
        SelectedTabIndex = index;
        await LoadCurrentTabAsync();
    }

    private async Task LoadSummaryAsync()
    {
        try
        {
            var rows = await _creditService.GetDailyCreditSummaryAsync(SummaryFromDate, SummaryToDate);
            SummaryNewCredit = rows.Sum(r => r.CreditSales);
            SummaryPayments = rows.Sum(r => r.PaymentsCollected);

            SummaryRows.Clear();
            foreach (var row in rows)
                SummaryRows.Add(row);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to load summary: {ex.Message}", "OK");
        }
    }

    private async Task LoadAgingAsync()
    {
        try
        {
            var rows = _cachedAging ?? await _creditService.GetCreditAgingAsync();

            AgingRows.Clear();
            foreach (var row in rows)
                AgingRows.Add(row);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to load aging report: {ex.Message}", "OK");
        }
    }

    private async Task LoadPaymentsAsync()
    {
        try
        {
            var rows = await _creditService.GetPaymentHistoryAsync(HistoryFromDate, HistoryToDate);

            PaymentRows.Clear();
            foreach (var row in rows)
                PaymentRows.Add(row);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to load payment history: {ex.Message}", "OK");
        }
    }

    private async Task LoadTrendAsync()
    {
        try
        {
            var points = await _creditService.GetCreditTrendAsync(30);

            TrendPoints.Clear();
            foreach (var pt in points)
                TrendPoints.Add(pt);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to load trend data: {ex.Message}", "OK");
        }
    }

    private async Task ExportCurrentTabAsync()
    {
        if (IsExporting)
            return;

        IsExporting = true;
        try
        {
            switch (SelectedTabIndex)
            {
                case 0: await ExportSummaryAsync(); break;
                case 1: await ExportAgingAsync(); break;
                case 2: await ExportPaymentsAsync(); break;
                default:
                    await Shell.Current.DisplayAlertAsync("Export", "Nothing to export for this tab.", "OK");
                    break;
            }
        }
        finally
        {
            IsExporting = false;
        }
    }

    private async Task ExportSummaryAsync()
    {
        var rows = new List<string[]>(SummaryRows.Count);
        foreach (var r in SummaryRows)
            rows.Add(new[]
            {
                r.Date.ToString("yyyy-MM-dd"),
                CreditReportExportService.FormatN(r.CreditSales),
                CreditReportExportService.FormatN(r.PaymentsCollected),
                CreditReportExportService.FormatN(r.NetOutstanding)
            });

        if (rows.Count == 0)
        {
            await Shell.Current.DisplayAlertAsync("Export", "No summary data to export.", "OK");
            return;
        }

        await CreditReportExportService.ExportAsync(
            "Credit Summary",
            $"credit_summary_{SummaryFromDate:yyyyMMdd}_{SummaryToDate:yyyyMMdd}.csv",
            new[] { "Date", "Credit Issued", "Collected", "Net Outstanding" },
            rows);
    }

    private async Task ExportAgingAsync()
    {
        var rows = new List<string[]>(AgingRows.Count);
        foreach (var r in AgingRows)
            rows.Add(new[]
            {
                r.CustomerName,
                r.Phone,
                CreditReportExportService.FormatN(r.TotalBalance),
                CreditReportExportService.FormatN(r.DailyBalance),
                CreditReportExportService.FormatN(r.PartnerBalance),
                r.LastPaymentDisplay,
                r.DaysSinceLastPayment.ToString(),
                r.AgingBucket
            });

        if (rows.Count == 0)
        {
            await Shell.Current.DisplayAlertAsync("Export", "No aging data to export.", "OK");
            return;
        }

        await CreditReportExportService.ExportAsync(
            "Credit Aging",
            $"credit_aging_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            new[] { "Customer", "Phone", "Total Balance", "Daily", "Partner", "Last Payment", "Days", "Bucket" },
            rows);
    }

    private async Task ExportPaymentsAsync()
    {
        var rows = new List<string[]>(PaymentRows.Count);
        foreach (var r in PaymentRows)
            rows.Add(new[]
            {
                r.PaymentDate.ToString("yyyy-MM-dd HH:mm"),
                r.CustomerName,
                CreditReportExportService.FormatN(r.Amount),
                r.BookType,
                r.Notes
            });

        if (rows.Count == 0)
        {
            await Shell.Current.DisplayAlertAsync("Export", "No payment data to export.", "OK");
            return;
        }

        await CreditReportExportService.ExportAsync(
            "Payment History",
            $"payments_{HistoryFromDate:yyyyMMdd}_{HistoryToDate:yyyyMMdd}.csv",
            new[] { "Date", "Customer", "Amount", "Book", "Notes" },
            rows);
    }
}
