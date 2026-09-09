using System.Windows.Input;
using DigiPasal.Models;
using DigiPasal.Services;

namespace DigiPasal.ViewModels;

public class QuickSaleViewModel : BaseViewModel
{
    private readonly SaleService _saleService;
    private readonly CustomerService _customerService;

    private decimal _amount;
    private string _amountText = string.Empty;
    private Customer? _selectedCustomer;
    private bool _isCreditSale;
    private string _amountPaid = string.Empty;
    private string _notes = string.Empty;

    public decimal Amount
    {
        get => _amount;
        set
        {
            SetProperty(ref _amount, value);
            OnPropertyChanged(nameof(AmountDisplay));
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(CreditAmount));
            OnPropertyChanged(nameof(CreditDisplay));
        }
    }

    public string AmountText
    {
        get => _amountText;
        set
        {
            if (SetProperty(ref _amountText, value))
            {
                if (decimal.TryParse(value, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var amt) && amt > 0)
                {
                    _amount = amt;
                    OnPropertyChanged(nameof(Amount));
                    OnPropertyChanged(nameof(AmountDisplay));
                    OnPropertyChanged(nameof(CanComplete));
                    OnPropertyChanged(nameof(CreditAmount));
                    OnPropertyChanged(nameof(CreditDisplay));
                }
            }
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
            OnPropertyChanged(nameof(CanComplete));
        }
    }

    public bool IsCreditSale
    {
        get => _isCreditSale;
        set
        {
            SetProperty(ref _isCreditSale, value);
            OnPropertyChanged(nameof(CanComplete));
            OnPropertyChanged(nameof(CreditAmount));
            OnPropertyChanged(nameof(CreditDisplay));
            CalculateTotals();
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

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public bool HasCustomer => SelectedCustomer != null;
    public bool CanComplete => Amount > 0 && (!IsCreditSale || HasCustomer);

    public string AmountDisplay => CurrencyFormatter.Format(Amount);

    public string SelectedCustomerDisplay => SelectedCustomer != null
        ? $"{SelectedCustomer.Name} (Balance: {CurrencyFormatter.Format(SelectedCustomer.CurrentBalance)})"
        : "Walk-in (Cash)";

    public decimal CreditAmount => IsCreditSale && decimal.TryParse(AmountPaid, out var paid)
        ? Math.Max(0, Amount - paid) : 0;

    public string CreditDisplay => IsCreditSale ? CurrencyFormatter.Format(CreditAmount) : "";

    public ICommand SelectCustomerCommand { get; }
    public ICommand SetQuickAmountCommand { get; }
    public ICommand CompleteSaleCommand { get; }

    public QuickSaleViewModel()
    {
        _saleService = SaleService.Instance;
        _customerService = CustomerService.Instance;
        Title = "Quick Sale";

        SelectCustomerCommand = new Command(async () => await SelectCustomerAsync());
        SetQuickAmountCommand = new Command<decimal>(amount => AmountText = amount.ToString());
        CompleteSaleCommand = new Command(async () => await CompleteSaleAsync());
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

    private void CalculateTotals()
    {
        OnPropertyChanged(nameof(CreditAmount));
        OnPropertyChanged(nameof(CreditDisplay));
    }

    private async Task CompleteSaleAsync()
    {
        if (Amount <= 0)
        {
            await Shell.Current.DisplayAlertAsync("Invalid Amount", "Please enter a sale amount.", "OK");
            return;
        }

        if (IsCreditSale && !HasCustomer)
        {
            await Shell.Current.DisplayAlertAsync("Credit Sale", "Please select a customer for credit sales.", "OK");
            return;
        }

        var paidAmount = IsCreditSale && decimal.TryParse(AmountPaid, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : Amount;

        if (IsCreditSale && paidAmount >= Amount)
        {
            await Shell.Current.DisplayAlertAsync("Credit Sale", "Amount paid equals or exceeds total. Use cash payment instead.", "OK");
            return;
        }

        var user = await AuthService.Instance.GetCurrentUserAsync();

        var sale = new Sale
        {
            Subtotal = Amount,
            DiscountAmount = 0,
            TaxAmount = 0,
            TotalAmount = Amount,
            GrandTotal = Amount,
            AmountPaid = paidAmount,
            CreditAmount = Amount - paidAmount,
            PaymentMethod = IsCreditSale ? "Credit" : "Cash",
            IsCredit = IsCreditSale,
            IsQuickSale = true,
            CustomerId = SelectedCustomer?.Id,
            CustomerName = SelectedCustomer?.Name ?? "Walk-in",
            UserId = user?.Id ?? 0,
            Notes = Notes
        };

        try
        {
            var createdSale = await _saleService.CreateQuickSaleAsync(sale);

            Amount = 0;
            AmountText = string.Empty;
            SelectedCustomer = null;
            IsCreditSale = false;
            AmountPaid = string.Empty;
            Notes = string.Empty;

            await Shell.Current.GoToAsync($"SaleSuccess?id={createdSale.Id}");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to complete sale: {ex.Message}", "OK");
        }
    }
}
