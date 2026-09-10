using System.Collections.ObjectModel;
using System.Globalization;
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
    private bool _isRecording;

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

    public bool CanRecordPayment => !_isRecording && Balance > 0;

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
                return;

            Balance = await _creditService.GetOutstandingForBookAsync(_customerId, _bookType);

            History.Clear();
            var entries = await _creditService.GetCustomerHistoryAsync(_customerId, _bookType);
            foreach (var entry in entries)
                History.Add(entry);

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

        var amountStr = await Shell.Current.DisplayPromptAsync(
            "Record Payment",
            $"Enter payment amount towards {CustomerName}'s {BookNameDisplay} balance.",
            "Next",
            "Cancel",
            "0.00",
            12,
            Keyboard.Numeric);

        if (string.IsNullOrWhiteSpace(amountStr))
            return;

        if (!decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
        {
            await Shell.Current.DisplayAlertAsync("Invalid Amount", "Enter a valid amount greater than zero.", "OK");
            return;
        }

        var confirm = await Shell.Current.DisplayAlertAsync(
            "Confirm Payment",
            $"Record {CurrencyFormatter.Format(amount)} from {CustomerName} ({BookNameDisplay} book)?",
            "Record",
            "Cancel");

        if (!confirm)
            return;

        _isRecording = true;
        OnPropertyChanged(nameof(CanRecordPayment));
        try
        {
            await _creditService.RecordPaymentAsync(_customerId, amount, _bookType);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to record payment: {ex.Message}", "OK");
        }
        finally
        {
            _isRecording = false;
            OnPropertyChanged(nameof(CanRecordPayment));
        }
    }
}