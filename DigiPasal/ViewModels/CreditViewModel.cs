using System.Collections.ObjectModel;
using System.Windows.Input;
using DigiPasal.Models;
using DigiPasal.Services;

namespace DigiPasal.ViewModels;

public class CreditViewModel : BaseViewModel
{
    private readonly CreditService _creditService;
    private readonly CustomerService _customerService;

    private bool _isDailyTab = true;
    private DateTime _selectedDate = DateTime.Today;
    private bool _isBookClosed;
    private bool _isClosing;
    private decimal _dailyOutstanding;
    private decimal _partnerOutstanding;
    private string _searchText = string.Empty;

    private List<DailyBookEntry> _allDailyEntries = new();
    private List<PartnerCreditItem> _allPartnerItems = new();

    public ObservableCollection<DailyBookEntry> DailyEntries { get; } = new();
    public ObservableCollection<PartnerCreditItem> PartnerItems { get; } = new();

    public CreditViewModel()
    {
        _creditService = CreditService.Instance;
        _customerService = CustomerService.Instance;
        Title = "Credit";

        SwitchToDailyCommand = new Command(async () => await SwitchToDailyAsync());
        SwitchToPartnerCommand = new Command(async () => await SwitchToPartnerAsync());
        CloseDailyBookCommand = new Command(async () => await CloseDailyBookAsync());
        ViewVoucherCommand = new Command(async () => await ViewVoucherAsync());
        PreviousDayCommand = new Command(async () => { if (SelectedDate > DateTime.MinValue) { SelectedDate = SelectedDate.AddDays(-1); await LoadDailyAsync(); } });
        NextDayCommand = new Command(async () => { if (SelectedDate.Date < DateTime.Today) { SelectedDate = SelectedDate.AddDays(1); await LoadDailyAsync(); } });
        TodayCommand = new Command(async () => { SelectedDate = DateTime.Today; await LoadDailyAsync(); });
        OpenReportsCommand = new Command(async () => await NavigationGuard.GoToAsync("CreditReports"));
        SearchCommand = new Command(() => ApplySearchFilter());
        ClearSearchCommand = new Command(async () => { SearchText = string.Empty; ApplySearchFilter(); });
    }

    public bool IsDailyTab
    {
        get => _isDailyTab;
        set
        {
            if (SetProperty(ref _isDailyTab, value))
            {
                OnPropertyChanged(nameof(IsPartnerTab));
                OnPropertyChanged(nameof(CanCloseBook));
                OnPropertyChanged(nameof(ShowCloseBook));
                OnPropertyChanged(nameof(ShowViewVoucher));
            }
        }
    }

    public bool IsPartnerTab => !IsDailyTab;

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (SetProperty(ref _selectedDate, value))
                OnPropertyChanged(nameof(SelectedDateDisplay));
        }
    }

    public string SelectedDateDisplay => NepaliDateConverter.Format(SelectedDate);

    public decimal DailyOutstanding
    {
        get => _dailyOutstanding;
        set
        {
            SetProperty(ref _dailyOutstanding, value);
            OnPropertyChanged(nameof(DailyOutstandingDisplay));
        }
    }

    public string DailyOutstandingDisplay => CurrencyFormatter.Format(DailyOutstanding);

    public decimal PartnerOutstanding
    {
        get => _partnerOutstanding;
        set
        {
            SetProperty(ref _partnerOutstanding, value);
            OnPropertyChanged(nameof(PartnerOutstandingDisplay));
        }
    }

    public string PartnerOutstandingDisplay => CurrencyFormatter.Format(PartnerOutstanding);

    public bool IsBookClosed
    {
        get => _isBookClosed;
        set
        {
            if (SetProperty(ref _isBookClosed, value))
            {
                OnPropertyChanged(nameof(CanCloseBook));
                OnPropertyChanged(nameof(BookStatusLabel));
                OnPropertyChanged(nameof(ShowCloseBook));
                OnPropertyChanged(nameof(ShowViewVoucher));
                OnPropertyChanged(nameof(CanViewVoucher));
            }
        }
    }

    public bool CanCloseBook => IsDailyTab && SelectedDate.Date <= DateTime.Today && !IsBookClosed && !_isClosing;

    public bool ShowCloseBook => IsDailyTab && !IsBookClosed;
    public bool ShowViewVoucher => IsDailyTab && IsBookClosed;
    public bool CanViewVoucher => IsBookClosed;

    public string BookStatusLabel => IsBookClosed
        ? "Daily book is closed — see the Day Voucher for this day's balances"
        : "Daily book is open";

    public bool HasDailyEntries => DailyEntries.Count > 0;
    public bool HasPartnerItems => PartnerItems.Count > 0;
    public string DailyEmptyMessage => IsBookClosed
        ? "No credit sales recorded for this day."
        : "No credit sales yet today. Credit sales during the day appear here.";

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                ApplySearchFilter();
        }
    }

    public bool HasSearchResults => !string.IsNullOrWhiteSpace(SearchText);
    public string SearchResultInfo => IsDailyTab
        ? $"{DailyEntries.Count} of {_allDailyEntries.Count} daily entries match"
        : $"{PartnerItems.Count} of {_allPartnerItems.Count} partners match";

    public ICommand SwitchToDailyCommand { get; }
    public ICommand SwitchToPartnerCommand { get; }
    public ICommand CloseDailyBookCommand { get; }
    public ICommand ViewVoucherCommand { get; }
    public ICommand PreviousDayCommand { get; }
    public ICommand NextDayCommand { get; }
    public ICommand TodayCommand { get; }
    public ICommand OpenReportsCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand ClearSearchCommand { get; }

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            if (IsDailyTab)
                await LoadDailyAsync();
            else
                await LoadPartnerAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SwitchToDailyAsync()
    {
        IsDailyTab = true;
        await LoadDailyAsync();
    }

    private async Task SwitchToPartnerAsync()
    {
        IsDailyTab = false;
        OnPropertyChanged(nameof(CanCloseBook));
        await LoadPartnerAsync();
    }

    private async Task LoadDailyAsync()
    {
        try
        {
            var report = await _creditService.GetDailyBookReportAsync(SelectedDate);
            IsBookClosed = report.IsClosed;
            DailyOutstanding = report.OutstandingTotal;

            _allDailyEntries = report.Entries.ToList();
            ApplyDailyFilter();

            OnPropertyChanged(nameof(HasDailyEntries));
            OnPropertyChanged(nameof(DailyEmptyMessage));
            OnPropertyChanged(nameof(CanCloseBook));
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to load daily book: {ex.Message}", "OK");
        }
    }

    private async Task LoadPartnerAsync()
    {
        try
        {
            var customers = await _creditService.GetPartnerCustomersAsync();
            PartnerOutstanding = customers.Sum(c => c.PartnerBalance);

            _allPartnerItems = customers.Select(c => new PartnerCreditItem(c)).ToList();
            ApplyPartnerFilter();

            OnPropertyChanged(nameof(HasPartnerItems));
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to load partners book: {ex.Message}", "OK");
        }
    }

    private void ApplySearchFilter()
    {
        if (IsDailyTab)
            ApplyDailyFilter();
        else
            ApplyPartnerFilter();

        OnPropertyChanged(nameof(HasSearchResults));
        OnPropertyChanged(nameof(SearchResultInfo));
    }

    private void ApplyDailyFilter()
    {
        DailyEntries.Clear();
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allDailyEntries
            : _allDailyEntries.Where(e =>
                e.CustomerName.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .ToList();

        foreach (var entry in filtered)
            DailyEntries.Add(entry);

        OnPropertyChanged(nameof(HasDailyEntries));
        OnPropertyChanged(nameof(DailyEmptyMessage));
    }

    private void ApplyPartnerFilter()
    {
        PartnerItems.Clear();
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allPartnerItems
            : _allPartnerItems.Where(p =>
                p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                (p.Phone != null && p.Phone.Contains(SearchText, StringComparison.OrdinalIgnoreCase)))
                .ToList();

        foreach (var item in filtered)
            PartnerItems.Add(item);

        OnPropertyChanged(nameof(HasPartnerItems));
    }

    private async Task CloseDailyBookAsync()
    {
        var confirm = await Shell.Current.DisplayAlertAsync(
            "Close Daily Book",
            $"Move all remaining daily credit to the Partners book for {NepaliDateConverter.Format(SelectedDate)}? This cannot be undone.",
            "Close Book", "Cancel");

        if (!confirm)
            return;

        _isClosing = true;
        OnPropertyChanged(nameof(CanCloseBook));
        try
        {
            var count = await _creditService.CloseDailyBookAsync(SelectedDate);

            if (count > 0)
            {
                await LoadAsync();
                await Shell.Current.DisplayAlertAsync(
                    "Daily Book Closed",
                    $"{count} customer(s) carried forward to the Partners book.",
                    "View Voucher");
            }
            else
            {
                await LoadAsync();
                await Shell.Current.DisplayAlertAsync(
                    "Daily Book Closed",
                    "Nothing to carry — all daily credit is settled.",
                    "View Voucher");
            }

await NavigationGuard.GoToAsync($"DayVoucher?date={SelectedDate:yyyy-MM-dd}");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to close daily book: {ex.Message}", "OK");
        }
        finally
        {
            _isClosing = false;
            OnPropertyChanged(nameof(CanCloseBook));
        }
    }

    private async Task ViewVoucherAsync()
    {
        await NavigationGuard.GoToAsync($"DayVoucher?date={SelectedDate:yyyy-MM-dd}");
    }

    public async Task NavigateToCustomerAsync(DailyBookEntry? dailyEntry, PartnerCreditItem? partner)
    {
        if (dailyEntry != null)
        {
            await NavigationGuard.GoToAsync($"CreditDetail?customerId={dailyEntry.CustomerIdValue}&book={(int)CreditBookType.Daily}");
        }
        else if (partner != null)
        {
            await NavigationGuard.GoToAsync($"CreditDetail?customerId={partner.CustomerId}&book={(int)CreditBookType.Partner}");
        }
    }
}

public class PartnerCreditItem
{
    public PartnerCreditItem(Customer customer)
    {
        CustomerId = customer.Id;
        Name = customer.Name;
        Phone = customer.Phone;
        PartnerBalance = customer.PartnerBalance;
    }

    public int CustomerId { get; }
    public string Name { get; }
    public string Phone { get; }
    public decimal PartnerBalance { get; }
    public string BalanceDisplay => CurrencyFormatter.Format(PartnerBalance);
    public string Subtitle => string.IsNullOrWhiteSpace(Phone) ? "Partner" : $"{Phone} · Partner";
}