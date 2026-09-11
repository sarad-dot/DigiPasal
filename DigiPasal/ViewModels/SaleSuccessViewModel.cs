using System.Windows.Input;
using DigiPasal.Models;
using DigiPasal.Services;

namespace DigiPasal.ViewModels;

public class SaleSuccessViewModel : BaseViewModel, IQueryAttributable
{
    private readonly SaleService _saleService;
    private readonly ReceiptService _receiptService;

    private Sale? _sale;
    private List<SaleItem> _saleItems = new();
    private string _receiptText = string.Empty;

    public Sale? Sale
    {
        get => _sale;
        set => SetProperty(ref _sale, value);
    }

    public string ReceiptText
    {
        get => _receiptText;
        set => SetProperty(ref _receiptText, value);
    }

    public bool IsCreditSale => Sale?.IsCredit ?? false;
    public string CreditInfo => IsCreditSale && Sale != null
        ? $"Balance Due: {CurrencyFormatter.Format(Sale.CreditAmount)} — {Sale.CustomerName}"
        : string.Empty;

    public ICommand ShareReceiptCommand { get; }
    public ICommand NewSaleCommand { get; }
    public ICommand ViewHistoryCommand { get; }
    public ICommand GoHomeCommand { get; }

    public SaleSuccessViewModel()
    {
        _saleService = SaleService.Instance;
        _receiptService = ReceiptService.Instance;
        Title = "Sale Complete";

        ShareReceiptCommand = new Command(async () => await ShareReceiptAsync());
        NewSaleCommand = new Command(async () => await NavigationGuard.GoToAsync("//Dashboard/Sales"));
        GoHomeCommand = new Command(async () => await NavigationGuard.GoToAsync("//Dashboard"));
        ViewHistoryCommand = new Command(async () => await NavigationGuard.GoToAsync("//Dashboard/SalesHistory"));
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
            if (Sale == null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Sale Not Found",
                    "This sale record no longer exists. It may have been deleted.",
                    "OK");
                await Shell.Current.GoToAsync("//Dashboard");
                return;
            }

            _saleItems = await _saleService.GetSaleItemsAsync(saleId);
            ReceiptText = await _receiptService.GenerateReceiptTextAsync(Sale, _saleItems);

            OnPropertyChanged(nameof(IsCreditSale));
            OnPropertyChanged(nameof(CreditInfo));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ShareReceiptAsync()
    {
        if (Sale == null) return;
        await _receiptService.ShareReceiptAsync(Sale, _saleItems);
    }
}
