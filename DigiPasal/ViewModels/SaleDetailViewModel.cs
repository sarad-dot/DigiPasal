using System.Windows.Input;
using DigiPasal.Models;
using DigiPasal.Services;

namespace DigiPasal.ViewModels;

public class SaleDetailViewModel : BaseViewModel, IQueryAttributable
{
    private readonly SaleService _saleService;
    private readonly ReceiptService _receiptService;

    private Sale? _sale;
    private List<SaleItem> _items = new();
    private string _receiptText = string.Empty;

    public Sale? Sale
    {
        get => _sale;
        set => SetProperty(ref _sale, value);
    }

    public List<SaleItem> Items
    {
        get => _items;
        set => SetProperty(ref _items, value);
    }

    public string ReceiptText
    {
        get => _receiptText;
        set => SetProperty(ref _receiptText, value);
    }

    public string TotalDisplay => Sale != null ? CurrencyFormatter.Format(Sale.GrandTotal) : "रु 0.00";
    public string StatusBadge => Sale?.IsCredit ?? false ? "Credit" : "Cash";
    public bool IsCreditSale => Sale?.IsCredit ?? false;

    public ICommand ShareReceiptCommand { get; }
    public ICommand VoidSaleCommand { get; }

    public SaleDetailViewModel()
    {
        _saleService = SaleService.Instance;
        _receiptService = ReceiptService.Instance;
        Title = "Sale Detail";

        ShareReceiptCommand = new Command(async () => await ShareReceiptAsync());
        VoidSaleCommand = new Command(async () => await VoidSaleAsync());
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var idObj) && idObj is string idStr && int.TryParse(idStr, out var id))
        {
            _ = LoadSaleAsync(id);
        }
    }

    private async Task LoadSaleAsync(int saleId)
    {
        IsBusy = true;
        try
        {
            Sale = await _saleService.GetSaleByIdAsync(saleId);
            if (Sale == null) return;

            Items = await _saleService.GetSaleItemsAsync(saleId);
            ReceiptText = await _receiptService.GenerateReceiptTextAsync(Sale, Items);

            OnPropertyChanged(nameof(TotalDisplay));
            OnPropertyChanged(nameof(StatusBadge));
            OnPropertyChanged(nameof(IsCreditSale));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ShareReceiptAsync()
    {
        if (Sale == null) return;
        await _receiptService.ShareReceiptAsync(Sale, Items);
    }

    private async Task VoidSaleAsync()
    {
        if (Sale == null) return;

        var confirmed = await Shell.Current.DisplayAlert(
            "Void Sale",
            $"Are you sure you want to void sale {Sale.ReceiptNumber}?\nThis will restore stock levels.",
            "Void Sale",
            "Cancel");

        if (!confirmed) return;

        var reason = await Shell.Current.DisplayPromptAsync(
            "Void Reason",
            "Enter reason for voiding this sale:",
            "OK",
            "Cancel",
            "Enter reason...",
            maxLength: 200);

        if (string.IsNullOrWhiteSpace(reason)) return;

        var success = await _saleService.VoidSaleAsync(Sale.Id, reason);
        if (success)
        {
            await LoadSaleAsync(Sale.Id);
            await Shell.Current.DisplayAlert("Sale Voided", "Stock has been restored.", "OK");
        }
    }
}
