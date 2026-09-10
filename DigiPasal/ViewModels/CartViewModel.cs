using System.Collections.ObjectModel;
using System.Windows.Input;
using DigiPasal.Models;
using DigiPasal.Services;

namespace DigiPasal.ViewModels;

public class CartViewModel : BaseViewModel
{
    private readonly SaleService _saleService;
    private readonly CustomerService _customerService;
    private readonly ReceiptService _receiptService;
    private readonly CartState _cartState;

    private Customer? _selectedCustomer;
    private bool _isCreditSale;
    private string _creditCustomerName = string.Empty;
    private string _amountPaid = string.Empty;
    private string _notes = string.Empty;
    private decimal _discountAmount;
    private decimal _taxRate;
    private decimal _subtotal;
    private decimal _taxAmount;
    private decimal _grandTotal;

    public ObservableCollection<CartLine> CartItems => _cartState.Items;

    public Customer? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            SetProperty(ref _selectedCustomer, value);
            OnPropertyChanged(nameof(SelectedCustomerDisplay));
            OnPropertyChanged(nameof(HasCustomer));
            OnPropertyChanged(nameof(IsCreditEnabled));
            OnPropertyChanged(nameof(ShowCreditNameField));
        }
    }

    public bool IsCreditSale
    {
        get => _isCreditSale;
        set
        {
            if (SetProperty(ref _isCreditSale, value) && value)
                AmountPaid = "0";
            OnPropertyChanged(nameof(ShowCreditNameField));
            OnPropertyChanged(nameof(IsCreditEnabled));
            CalculateTotals();
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
            CalculateTotals();
        }
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public decimal DiscountAmount
    {
        get => _discountAmount;
        set
        {
            SetProperty(ref _discountAmount, value);
            CalculateTotals();
        }
    }

    public decimal Subtotal
    {
        get => _subtotal;
        set => SetProperty(ref _subtotal, value);
    }

    public decimal TaxAmount
    {
        get => _taxAmount;
        set => SetProperty(ref _taxAmount, value);
    }

    public decimal GrandTotal
    {
        get => _grandTotal;
        set => SetProperty(ref _grandTotal, value);
    }

    public decimal CreditAmount => IsCreditSale
        ? Math.Max(0, GrandTotal - (decimal.TryParse(AmountPaid, out var paid) ? paid : 0))
        : 0;

    public string SelectedCustomerDisplay => SelectedCustomer != null
        ? $"{SelectedCustomer.Name} (Balance: {CurrencyFormatter.Format(SelectedCustomer.CurrentBalance)})"
        : "Walk-in (Cash)";

    public bool HasItems => CartItems.Count > 0;
    public int ItemCount => CartItems.Count;
    public bool HasCustomer => SelectedCustomer != null;
    public bool IsCreditEnabled => IsCreditSale;
    public bool ShowCreditNameField => IsCreditSale && !HasCustomer;

    public string SubtotalDisplay => CurrencyFormatter.Format(Subtotal);
    public string DiscountDisplay => DiscountAmount > 0 ? $"-{CurrencyFormatter.Format(DiscountAmount)}" : "";
    public string TaxDisplay => TaxAmount > 0 ? CurrencyFormatter.Format(TaxAmount) : "";
    public string GrandTotalDisplay => CurrencyFormatter.Format(GrandTotal);
    public string CreditDisplay => IsCreditSale ? CurrencyFormatter.Format(CreditAmount) : "";

    public ICommand SelectCustomerCommand { get; }
    public ICommand RemoveItemCommand { get; }
    public ICommand IncrementQuantityCommand { get; }
    public ICommand DecrementQuantityCommand { get; }
    public ICommand CheckoutCommand { get; }
    public ICommand ClearCartCommand { get; }

    public CartViewModel()
    {
        _saleService = SaleService.Instance;
        _customerService = CustomerService.Instance;
        _receiptService = ReceiptService.Instance;
        _cartState = CartState.Instance;
        Title = "Cart";

        _cartState.CartChanged += OnCartChanged;

        SelectCustomerCommand = new Command(async () => await SelectCustomerAsync());
        RemoveItemCommand = new Command<CartLine>(RemoveItem);
        IncrementQuantityCommand = new Command<CartLine>(IncrementQuantity);
        DecrementQuantityCommand = new Command<CartLine>(DecrementQuantity);
        CheckoutCommand = new Command(async () => await CheckoutAsync());
        ClearCartCommand = new Command(ClearCart);
    }

    public void Cleanup()
    {
        _cartState.CartChanged -= OnCartChanged;
    }

    private void OnCartChanged()
    {
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(ItemCount));
        CalculateTotals();
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

            CalculateTotals();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to load customers: {ex.Message}", "OK");
        }
    }

    private void RemoveItem(CartLine? item)
    {
        if (item == null) return;
        _cartState.RemoveItem(item);
    }

    private void IncrementQuantity(CartLine? item)
    {
        if (item == null) return;
        _cartState.IncrementQuantity(item);
    }

    private void DecrementQuantity(CartLine? item)
    {
        if (item == null) return;
        _cartState.DecrementQuantity(item);
    }

    private void ClearCart()
    {
        _cartState.Clear();
        SelectedCustomer = null;
        IsCreditSale = false;
        CreditCustomerName = string.Empty;
        AmountPaid = string.Empty;
        Notes = string.Empty;
        DiscountAmount = 0;
    }

    private void CalculateTotals()
    {
        Subtotal = CartItems.Sum(c => c.LineTotal);

        var afterDiscount = Subtotal - DiscountAmount;
        TaxAmount = afterDiscount * _taxRate / 100m;
        GrandTotal = afterDiscount + TaxAmount;

        OnPropertyChanged(nameof(SubtotalDisplay));
        OnPropertyChanged(nameof(DiscountDisplay));
        OnPropertyChanged(nameof(TaxDisplay));
        OnPropertyChanged(nameof(GrandTotalDisplay));
        OnPropertyChanged(nameof(CreditAmount));
        OnPropertyChanged(nameof(CreditDisplay));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(ItemCount));
    }

    public async Task LoadTaxRateAsync()
    {
        var taxStr = await DatabaseService.Instance.GetSettingAsync("tax_rate");
        if (decimal.TryParse(taxStr, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var rate))
        {
            _taxRate = rate;
            CalculateTotals();
        }
    }

    private async Task CheckoutAsync()
    {
        if (!HasItems)
        {
            await Shell.Current.DisplayAlertAsync("Empty Cart", "Add products to the cart before checkout.", "OK");
            return;
        }

        var paidAmount = decimal.TryParse(AmountPaid, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var p) ? p
            : IsCreditSale ? 0 : GrandTotal;

        if (IsCreditSale && paidAmount >= GrandTotal)
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
            Subtotal = Subtotal,
            DiscountAmount = DiscountAmount,
            TaxAmount = TaxAmount,
            TotalAmount = Subtotal - DiscountAmount,
            GrandTotal = GrandTotal,
            AmountPaid = paidAmount,
            CreditAmount = GrandTotal - paidAmount,
            PaymentMethod = IsCreditSale ? "Credit" : "Cash",
            IsCredit = IsCreditSale,
            CreditBookType = (int)CreditBookType.Daily,
            CustomerId = IsCreditSale ? creditCustomerId : SelectedCustomer?.Id ?? (int?)null,
            CustomerName = IsCreditSale ? creditCustomerName : SelectedCustomer?.Name ?? "Walk-in",
            UserId = user?.Id ?? 0,
            Notes = Notes
        };

        var items = CartItems.Select(c => new SaleItem
        {
            ProductId = c.ProductId,
            ProductName = c.ProductName ?? string.Empty,
            Unit = c.Unit ?? "pcs",
            UnitPrice = c.UnitPrice,
            Quantity = c.Quantity,
            LineTotal = c.LineTotal
        }).ToList();

        try
        {
            var createdSale = await _saleService.CreateSaleAsync(sale, items);

            ClearCart();

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
