using System.Collections.ObjectModel;
using System.Windows.Input;
using DigiPasal.Models;
using DigiPasal.Services;

namespace DigiPasal.ViewModels;

public class CreditDetailViewModel : BaseViewModel
{
    private readonly CreditService _creditService;
    private readonly CustomerService _customerService;

    private int _customerId;
    private CreditBookType _bookType = CreditBookType.Daily;
    private Customer? _customer;
    private decimal _balance;
    private decimal _totalSales;
    private decimal _totalPayments;
    private int _transactionCount;

    public ObservableCollection<CreditHistoryEntry> History { get; } = new();

    public CreditDetailViewModel()
    {
        _creditService = CreditService.Instance;
        _customerService = CustomerService.Instance;
        Title = "Credit Detail";

        RecordPaymentCommand = new Command(async () => await RecordPaymentAsync());
    }

    public int CustomerId => _customerId;

    public Customer? Customer
    {
        get => _customer;
        private set
        {
            SetProperty(ref _customer, value);
            OnPropertyChanged(nameof(CustomerName));
            OnPropertyChanged(nameof(CustomerSubtitle));
        }
    }

    public string CustomerName => Customer?.Name ?? string.Empty;

    public string CustomerSubtitle
    {
        get
        {
            if (Customer == null)
                return string.Empty;

            var phone = string.IsNullOrWhiteSpace(Customer.Phone) ? "No phone" : Customer.Phone;
            return $"{phone} · {BookNameDisplay} book";
        }
    }

    public string BookNameDisplay => _bookType == CreditBookType.Partner ? "Partner" : "Daily";

    public decimal Balance
    {
        get => _balance;
        private set
        {
            SetProperty(ref _balance, value);
            OnPropertyChanged(nameof(BalanceDisplay));
        }
    }

    public string BalanceDisplay => CurrencyFormatter.Format(Balance);

    public bool HasHistory => History.Count > 0;

    public bool CanRecordPayment => Balance > 0;

    public decimal TotalSales
    {
        get => _totalSales;
        set { SetProperty(ref _totalSales, value); OnPropertyChanged(nameof(TotalSalesDisplay)); }
    }

    public decimal TotalPayments
    {
        get => _totalPayments;
        set { SetProperty(ref _totalPayments, value); OnPropertyChanged(nameof(TotalPaymentsDisplay)); }
    }

    public int TransactionCount
    {
        get => _transactionCount;
        set { SetProperty(ref _transactionCount, value); OnPropertyChanged(nameof(TransactionCountDisplay)); }
    }

    public string TotalSalesDisplay => CurrencyFormatter.Format(TotalSales);
    public string TotalPaymentsDisplay => CurrencyFormatter.Format(TotalPayments);
    public string TransactionCountDisplay => $"{TransactionCount} transactions";

    public ICommand RecordPaymentCommand { get; }

    public void SetParameters(int customerId, int book)
    {
        _customerId = customerId;
        _bookType = (CreditBookType)book;
        OnPropertyChanged(nameof(BookNameDisplay));
    }

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            Customer = await _customerService.GetCustomerAsync(_customerId);
            if (Customer == null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Customer Not Found",
                    "This customer no longer exists. It may have been removed.",
                    "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            Balance = await _creditService.GetOutstandingForBookAsync(_customerId, _bookType);

            History.Clear();
            var entries = await _creditService.GetCustomerHistoryAsync(_customerId, _bookType);
            foreach (var entry in entries)
                History.Add(entry);

            TotalSales = entries.Where(e => e.IsSale).Sum(e => e.Amount);
            TotalPayments = entries.Where(e => e.IsPayment).Sum(e => e.Amount);
            TransactionCount = entries.Count;

            OnPropertyChanged(nameof(HasHistory));
            OnPropertyChanged(nameof(CanRecordPayment));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RecordPaymentAsync()
    {
        if (Balance <= 0)
        {
            await Shell.Current.DisplayAlertAsync("Record Payment", $"No outstanding {BookNameDisplay} balance for this customer.", "OK");
            return;
        }

        await NavigationGuard.GoToAsync($"RecordPayment?customerId={_customerId}&book={(int)_bookType}");
    }
}