using System.Collections.ObjectModel;
using System.Windows.Input;
using DigiPasal.Services;

namespace DigiPasal.ViewModels;

public class SalesHistoryViewModel : BaseViewModel
{
    private readonly SaleService _saleService;

    private ObservableCollection<SaleGroup> _groups = new();
    private DateTime _dateFrom = DateTime.Today.AddDays(-30);
    private DateTime _dateTo = DateTime.Today;
    private string _searchQuery = string.Empty;
    private string _selectedPreset = string.Empty;
    private decimal _totalSales;
    private decimal _cashSales;
    private decimal _creditSales;
    private int _salesCount;
    private int _unitsCount;
    private bool _isCustomRange;
    private bool _isRefreshing;
    private readonly Debouncer _searchDebouncer = new();

    public ObservableCollection<SaleGroup> Groups
    {
        get => _groups;
        set => SetProperty(ref _groups, value);
    }

    public DateTime DateFrom
    {
        get => _dateFrom;
        set
        {
            if (SetProperty(ref _dateFrom, value.Date))
                _ = LoadSalesAsync();
        }
    }

    public DateTime DateTo
    {
        get => _dateTo;
        set
        {
            var clamped = value.Date > DateTime.Today ? DateTime.Today : value.Date;
            if (SetProperty(ref _dateTo, clamped))
                _ = LoadSalesAsync();
        }
    }

    public DateTime MaximumDate => DateTime.Today;

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
                _searchDebouncer.Debounce(300, LoadSalesAsync);
        }
    }

    public string SelectedPreset
    {
        get => _selectedPreset;
        set => SetProperty(ref _selectedPreset, value);
    }

    public bool IsCustomRange
    {
        get => _isCustomRange;
        set => SetProperty(ref _isCustomRange, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
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

    public int UnitsCount
    {
        get => _unitsCount;
        set => SetProperty(ref _unitsCount, value);
    }

    public string TotalSalesDisplay => CurrencyFormatter.Format(TotalSales);
    public string CashSalesDisplay => CurrencyFormatter.Format(CashSales);
    public string CreditSalesDisplay => CurrencyFormatter.Format(CreditSales);
    public string SalesCountDisplay => $"{SalesCount}";
    public string UnitsCountDisplay => $"{UnitsCount:N0}";

    public DateTime DateFromDefault => _dateFrom;
    public DateTime DateToDefault => _dateTo;

    public ICommand LoadSalesCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SelectPresetCommand { get; }
    public ICommand ViewSaleCommand { get; }

    public SalesHistoryViewModel()
    {
        _saleService = SaleService.Instance;
        Title = "Sales History";

        LoadSalesCommand = new Command(async () => await LoadSalesAsync());
        RefreshCommand = new Command(async () =>
        {
            IsRefreshing = true;
            try
            {
                await LoadSalesAsync();
            }
            finally
            {
                IsRefreshing = false;
            }
        });
        SelectPresetCommand = new Command<string>(async (preset) => await SelectPresetAsync(preset));
        ViewSaleCommand = new Command<SaleRow>(async (row) => await ViewSaleAsync(row));
    }

    private async Task SelectPresetAsync(string? preset)
    {
        switch (preset)
        {
            case "today":
                DateFrom = DateTime.Today;
                DateTo = DateTime.Today;
                IsCustomRange = false;
                break;
            case "week":
                DateFrom = DateTime.Today.AddDays(-7);
                DateTo = DateTime.Today;
                IsCustomRange = false;
                break;
            case "month":
                DateFrom = DateTime.Today.AddMonths(-1);
                DateTo = DateTime.Today;
                IsCustomRange = false;
                break;
            case "year":
                DateFrom = new DateTime(DateTime.Today.Year, 1, 1);
                DateTo = DateTime.Today;
                IsCustomRange = false;
                break;
            case "custom":
                IsCustomRange = true;
                break;
            default:
                return;
        }

        SelectedPreset = preset;
        await LoadSalesAsync();
    }

    public async Task LoadSalesAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var items = await _saleService.GetSalesListItemAsync(DateFrom, DateTo, SearchQuery);

            var rows = items.Select(ToRow).ToList();

            SalesCount = rows.Count;
            UnitsCount = items.Sum(i => i.UnitCount);
            TotalSales = items.Sum(i => i.GrandTotal);
            CashSales = items.Where(i => !i.IsCredit).Sum(i => i.GrandTotal);
            CreditSales = items.Where(i => i.IsCredit).Sum(i => i.CreditAmount);

            OnPropertyChanged(nameof(TotalSalesDisplay));
            OnPropertyChanged(nameof(CashSalesDisplay));
            OnPropertyChanged(nameof(CreditSalesDisplay));
            OnPropertyChanged(nameof(SalesCountDisplay));
            OnPropertyChanged(nameof(UnitsCountDisplay));

            var groups = rows
                .GroupBy(r => r.CreatedAt.Date)
                .OrderByDescending(g => g.Key)
                .Select(g => CreateGroup(g.Key, g))
                .ToList();

            Groups = new ObservableCollection<SaleGroup>(groups);

            UpdateSelectedPresetFromRange();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static SaleRow ToRow(SaleListItem s) => new()
    {
        SaleId = s.Id,
        ReceiptNumber = s.ReceiptNumber,
        CreatedAt = s.CreatedAt,
        CustomerDisplay = string.IsNullOrWhiteSpace(s.CustomerName) ? "Walk-in" : s.CustomerName,
        ItemCount = s.ItemCount,
        GrandTotal = s.GrandTotal,
        IsCredit = s.IsCredit,
        IsQuickSale = s.IsQuickSale,
        BadgeText = s.IsCredit ? "Credit" : s.IsQuickSale ? "Quick" : "Cash"
    };

    private static SaleGroup CreateGroup(DateTime day, IEnumerable<SaleRow> rows)
    {
        var list = rows.ToList();
        return new SaleGroup(list)
        {
            Title = GetDayTitle(day),
            TotalDisplay = CurrencyFormatter.Format(list.Sum(r => r.GrandTotal))
        };
    }

    private static string GetDayTitle(DateTime day)
    {
        if (day.Date == DateTime.Today)
            return "Today";
        if (day.Date == DateTime.Today.AddDays(-1))
            return "Yesterday";
        return day.ToString("dd MMMM yyyy");
    }

    private void UpdateSelectedPresetFromRange()
    {
        var from = DateFrom.Date;
        var to = DateTo.Date;
        var today = DateTime.Today;

        if (from == today && to == today)
            SelectedPreset = "today";
        else if (from == today.AddDays(-7) && to == today)
            SelectedPreset = "week";
        else if (from == today.AddMonths(-1) && to == today)
            SelectedPreset = "month";
        else if (from == new DateTime(today.Year, 1, 1) && to == today)
            SelectedPreset = "year";
        else
        {
            SelectedPreset = "custom";
            IsCustomRange = true;
        }
    }

    private async Task ViewSaleAsync(SaleRow? row)
    {
        if (row == null) return;
        await NavigationGuard.GoToAsync($"SaleDetail?id={row.SaleId}");
    }
}

public class SaleGroup : ObservableCollection<SaleRow>
{
    public SaleGroup()
    {
    }

    public SaleGroup(IEnumerable<SaleRow> rows)
        : base(rows)
    {
    }

    public string Title { get; set; } = string.Empty;
    public string TotalDisplay { get; set; } = string.Empty;
}

public class SaleRow
{
    public int SaleId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CustomerDisplay { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public string ItemCountDisplay => $"{ItemCount} {(ItemCount == 1 ? "item" : "items")}";
    public decimal GrandTotal { get; set; }
    public string GrandTotalDisplay => CurrencyFormatter.Format(GrandTotal);
    public bool IsCredit { get; set; }
    public bool IsQuickSale { get; set; }
    public string BadgeText { get; set; } = string.Empty;

    public string TimeDisplay => CreatedAt.ToString("hh:mm tt");

    public string SubtitleDisplay => $"{TimeDisplay}  ·  {ItemCountDisplay}  ·  {CustomerDisplay}";
}