using System.Globalization;
using System.Windows.Input;
using DigiPasal.Models;
using DigiPasal.Services;

namespace DigiPasal.ViewModels;

public class QuickSaleViewModel : BaseViewModel
{
    private readonly SaleService _saleService;
    private readonly CustomerService _customerService;

    private decimal _sum;
    private string _currentEntry = string.Empty;
    private Customer? _selectedCustomer;
    private bool _isCreditSale;
    private string _creditCustomerName = string.Empty;
    private string _amountPaid = string.Empty;
    private bool _entryJustAdded;

    public decimal Sum
    {
        get => _sum;
        private set
        {
            if (SetProperty(ref _sum, value))
                NotifyTotals();
        }
    }

    public string CurrentEntry
    {
        get => _currentEntry;
        private set
        {
            if (SetProperty(ref _currentEntry, value))
            {
                OnPropertyChanged(nameof(CurrentEntryDisplay));
                NotifyTotals();
            }
        }
    }

    public string CurrentEntryDisplay =>
        string.IsNullOrEmpty(CurrentEntry) ? "0" : CurrentEntry;

    /// <summary>Running sum plus any amount still being typed (what checkout charges).</summary>
    public decimal Amount => Sum + ParseEntry();

    public string AmountDisplay => CurrencyFormatter.Format(Amount);

    public string SumDisplay => CurrencyFormatter.Format(Sum);

    public string ExpressionHint
    {
        get
        {
            if (Sum <= 0 && string.IsNullOrEmpty(CurrentEntry))
                return "Enter amounts and tap + to add";
            if (Sum > 0 && !string.IsNullOrEmpty(CurrentEntry))
                return $"{CurrencyFormatter.Format(Sum)} + {CurrentEntry}";
            if (Sum > 0)
                return "Tap + to add another amount";
            return "Tap + to add to total";
        }
    }

    public Customer? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            SetProperty(ref _selectedCustomer, value);
            OnPropertyChanged(nameof(SelectedCustomerDisplay));
            OnPropertyChanged(nameof(HasCustomer));
            OnPropertyChanged(nameof(ShowCreditNameField));
            OnPropertyChanged(nameof(CanComplete));
        }
    }

    public bool IsCreditSale
    {
        get => _isCreditSale;
        set
        {
            SetProperty(ref _isCreditSale, value);
            OnPropertyChanged(nameof(ShowCreditNameField));
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(CreditAmount));
            OnPropertyChanged(nameof(CreditDisplay));
        }
    }

    public string CreditCustomerName
    {
        get => _creditCustomerName;
        set
        {
            SetProperty(ref _creditCustomerName, value);
            OnPropertyChanged(nameof(ShowCreditNameField));
        }
    }

    public string AmountPaid
    {
        get => _amountPaid;
        set
        {
            SetProperty(ref _amountPaid, value);
            OnPropertyChanged(nameof(CreditAmount));
            OnPropertyChanged(nameof(CreditDisplay));
            OnPropertyChanged(nameof(CanComplete));
        }
    }

    public bool HasCustomer => SelectedCustomer != null;
    public bool CanComplete => Amount > 0;
    public bool ShowCreditNameField => IsCreditSale && !HasCustomer;

    public string SelectedCustomerDisplay => SelectedCustomer != null
        ? $"{SelectedCustomer.Name} (Balance: {CurrencyFormatter.Format(SelectedCustomer.CurrentBalance)})"
        : "Walk-in (Cash)";

    public decimal CreditAmount => IsCreditSale && decimal.TryParse(AmountPaid, NumberStyles.Any,
        CultureInfo.InvariantCulture, out var paid)
        ? Math.Max(0, Amount - paid)
        : 0;

    public string CreditDisplay => IsCreditSale ? CurrencyFormatter.Format(CreditAmount) : "";

    public ICommand DigitCommand { get; }
    public ICommand DecimalCommand { get; }
    public ICommand BackspaceCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand AddToSumCommand { get; }
    public ICommand SelectCustomerCommand { get; }
    public ICommand CompleteSaleCommand { get; }

    public QuickSaleViewModel()
    {
        _saleService = SaleService.Instance;
        _customerService = CustomerService.Instance;
        Title = "Quick Sale";

        DigitCommand = new Command<string>(AppendDigit);
        DecimalCommand = new Command(AppendDecimal);
        BackspaceCommand = new Command(Backspace);
        ClearCommand = new Command(ClearAll);
        AddToSumCommand = new Command(AddToSum);
        SelectCustomerCommand = new Command(async () => await SelectCustomerAsync());
        CompleteSaleCommand = new Command(async () => await CompleteSaleAsync());
    }

    private void NotifyTotals()
    {
        OnPropertyChanged(nameof(Amount));
        OnPropertyChanged(nameof(AmountDisplay));
        OnPropertyChanged(nameof(SumDisplay));
        OnPropertyChanged(nameof(ExpressionHint));
        OnPropertyChanged(nameof(CanComplete));
        OnPropertyChanged(nameof(CreditAmount));
        OnPropertyChanged(nameof(CreditDisplay));
    }

    private decimal ParseEntry()
    {
        if (string.IsNullOrWhiteSpace(CurrentEntry))
            return 0;

        return decimal.TryParse(CurrentEntry, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private void AppendDigit(string? digit)
    {
        if (string.IsNullOrEmpty(digit))
            return;

        if (_entryJustAdded)
        {
            CurrentEntry = string.Empty;
            _entryJustAdded = false;
        }

        if (CurrentEntry == "0")
            CurrentEntry = digit;
        else if (CurrentEntry.Length < 12)
            CurrentEntry += digit;
    }

    private void AppendDecimal()
    {
        if (_entryJustAdded)
        {
            CurrentEntry = "0.";
            _entryJustAdded = false;
            return;
        }

        if (string.IsNullOrEmpty(CurrentEntry))
            CurrentEntry = "0.";
        else if (!CurrentEntry.Contains('.'))
            CurrentEntry += ".";
    }

    private void Backspace()
    {
        _entryJustAdded = false;
        if (string.IsNullOrEmpty(CurrentEntry))
            return;

        CurrentEntry = CurrentEntry.Length == 1
            ? string.Empty
            : CurrentEntry[..^1];
    }

    private void ClearAll()
    {
        Sum = 0;
        CurrentEntry = string.Empty;
        _entryJustAdded = false;
    }

    private void AddToSum()
    {
        var entry = ParseEntry();
        if (entry <= 0)
            return;

        Sum += entry;
        CurrentEntry = string.Empty;
        _entryJustAdded = true;
    }

    private void ResetCalculator()
    {
        Sum = 0;
        CurrentEntry = string.Empty;
        _entryJustAdded = false;
        SelectedCustomer = null;
        IsCreditSale = false;
        CreditCustomerName = string.Empty;
        AmountPaid = string.Empty;
    }

    private async Task SelectCustomerAsync()
    {
        try
        {
            var customers = await _customerService.GetActiveCustomersAsync();

            var options = new List<string> { "Walk-in (Cash)" };
            options.AddRange(customers.Select(c => $"{c.Name} ({CurrencyFormatter.Format(c.CurrentBalance)} due)"));

            var action = await Shell.Current.DisplayActionSheetAsync(
                "Select Customer",
                null,
                "Cancel",
                options.ToArray());

            if (action == null || action == "Cancel")
                return;

            if (action == "Walk-in (Cash)")
            {
                SelectedCustomer = null;
                IsCreditSale = false;
                AmountPaid = string.Empty;
            }
            else
            {
                var parenIndex = action.LastIndexOf(" (");
                var selectedName = parenIndex > 0 ? action[..parenIndex] : action;
                SelectedCustomer = customers.FirstOrDefault(c => c.Name == selectedName);
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to load customers: {ex.Message}", "OK");
        }
    }

    private async Task CompleteSaleAsync()
    {
        // Fold any pending entry into the sum before charging
        var pending = ParseEntry();
        if (pending > 0)
        {
            Sum += pending;
            CurrentEntry = string.Empty;
            _entryJustAdded = true;
        }

        if (Amount <= 0)
        {
            await Shell.Current.DisplayAlertAsync("Invalid Amount", "Add at least one amount before completing the sale.", "OK");
            return;
        }

        var saleAmount = Amount;
        var paidAmount = IsCreditSale && decimal.TryParse(AmountPaid, NumberStyles.Any,
            CultureInfo.InvariantCulture, out var p) ? p : saleAmount;

        if (IsCreditSale && paidAmount >= saleAmount)
        {
            await Shell.Current.DisplayAlertAsync("Credit Sale", "Amount paid equals or exceeds total. Use cash payment instead.", "OK");
            return;
        }

        var user = await AuthService.Instance.GetCurrentUserAsync();

        int? creditCustomerId = null;
        string creditCustomerName = "Walk-in";

        if (IsCreditSale)
        {
            var resolved = await ResolveCreditCustomerAsync();
            creditCustomerId = resolved.customerId;
            creditCustomerName = resolved.customerName;
        }

        var sale = new Sale
        {
            Subtotal = saleAmount,
            DiscountAmount = 0,
            TaxAmount = 0,
            TotalAmount = saleAmount,
            GrandTotal = saleAmount,
            AmountPaid = paidAmount,
            CreditAmount = saleAmount - paidAmount,
            PaymentMethod = IsCreditSale ? "Credit" : "Cash",
            IsCredit = IsCreditSale,
            IsQuickSale = true,
            CreditBookType = (int)CreditBookType.Daily,
            CustomerId = IsCreditSale ? creditCustomerId : null,
            CustomerName = IsCreditSale ? creditCustomerName : "Walk-in",
            UserId = user?.Id ?? 0,
            Notes = string.Empty
        };

        try
        {
            var createdSale = await _saleService.CreateQuickSaleAsync(sale);
            ResetCalculator();
            await Shell.Current.GoToAsync($"SaleSuccess?id={createdSale.Id}");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to complete sale: {ex.Message}", "OK");
        }
    }

    private async Task<(int customerId, string customerName)> ResolveCreditCustomerAsync()
    {
        if (SelectedCustomer != null)
            return (SelectedCustomer.Id, SelectedCustomer.Name);

        var name = string.IsNullOrWhiteSpace(CreditCustomerName) ? "Walk-in" : CreditCustomerName.Trim();

        var existing = await _customerService.FindActiveByNameAsync(name);
        if (existing != null)
            return (existing.Id, existing.Name);

        var created = new Customer
        {
            Name = name,
            IsActive = true
        };
        var id = await _customerService.SaveCustomerAsync(created);
        return (id, name);
    }
}
