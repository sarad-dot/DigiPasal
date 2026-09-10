using System;
using System.Globalization;
using System.Windows.Input;
using DigiPasal.Models;
using DigiPasal.Services;

namespace DigiPasal.ViewModels;

public class RecordPaymentViewModel : BaseViewModel
{
    private readonly CreditService _creditService;
    private readonly CustomerService _customerService;

    private int _customerId;
    private int _book;
    private string _customerName = string.Empty;
    private string _bookName = "Daily";
    private decimal _outstanding;
    private string _amountText = string.Empty;
    private string _notes = string.Empty;
    private string _errorText = string.Empty;
    private bool _isSaving;

    public RecordPaymentViewModel()
    {
        _creditService = CreditService.Instance;
        _customerService = CustomerService.Instance;

        SaveCommand = new Command(async () => await SaveAsync());
    }

    public string CustomerName
    {
        get => _customerName;
        private set => SetProperty(ref _customerName, value);
    }

    public string BookName
    {
        get => _bookName;
        private set => SetProperty(ref _bookName, value);
    }

    public decimal Outstanding
    {
        get => _outstanding;
        private set
        {
            SetProperty(ref _outstanding, value);
            OnPropertyChanged(nameof(OutstandingDisplay));
        }
    }

    public string OutstandingDisplay => CurrencyFormatter.Format(Outstanding);

    public string AmountText
    {
        get => _amountText;
        set
        {
            if (SetProperty(ref _amountText, value))
                Validate();
        }
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public string ErrorText
    {
        get => _errorText;
        private set
        {
            SetProperty(ref _errorText, value);
            OnPropertyChanged(nameof(HasValidationError));
        }
    }

    public bool HasValidationError => !string.IsNullOrWhiteSpace(ErrorText);

    public bool IsSaving
    {
        get => _isSaving;
        private set
        {
            SetProperty(ref _isSaving, value);
            OnPropertyChanged(nameof(CanSave));
        }
    }

    public bool CanSave => !_isSaving && !HasValidationError && !string.IsNullOrWhiteSpace(AmountText);

    public ICommand SaveCommand { get; }

    public void SetParameters(int customerId, int book)
    {
        _customerId = customerId;
        _book = book;
        BookName = book == (int)CreditBookType.Partner ? "Partner" : "Daily";
    }

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var customer = await _customerService.GetCustomerAsync(_customerId);
            if (customer == null)
            {
                ErrorText = "Customer not found.";
                return;
            }

            CustomerName = customer.Name;
            Outstanding = await _creditService.GetOutstandingForBookAsync(_customerId, (CreditBookType)_book);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private decimal? TryParseAmount()
    {
        if (decimal.TryParse(AmountText, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
            return amount;
        return null;
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(AmountText))
        {
            ErrorText = string.Empty;
            OnPropertyChanged(nameof(CanSave));
            return;
        }

        var amount = TryParseAmount();
        if (amount is null || amount <= 0)
        {
            ErrorText = "Enter a valid amount greater than zero.";
        }
        else if (amount > Outstanding)
        {
            ErrorText = $"Amount exceeds the outstanding {OutstandingDisplay}. Enter an amount up to the balance due.";
        }
        else
        {
            ErrorText = string.Empty;
        }

        OnPropertyChanged(nameof(CanSave));
    }

    private async Task SaveAsync()
    {
        var amount = TryParseAmount();
        if (amount is null || amount <= 0)
        {
            Validate();
            return;
        }

        IsSaving = true;
        try
        {
            var payment = await _creditService.RecordPaymentAsync(
                _customerId,
                amount.Value,
                (CreditBookType)_book,
                notes: string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim());

            await Shell.Current.DisplayAlertAsync(
                "Payment Recorded",
                $"{CurrencyFormatter.Format(payment.Amount)} received from {CustomerName}.",
                "OK");

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to record payment: {ex.Message}", "OK");
        }
        finally
        {
            IsSaving = false;
        }
    }
}