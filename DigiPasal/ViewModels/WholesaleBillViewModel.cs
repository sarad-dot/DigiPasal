using System.Collections.ObjectModel;
using DigiPasal.Services;

namespace DigiPasal.ViewModels;

public class WholesaleBillViewModel : BaseViewModel
{
    private readonly PurchaseService _purchaseService;
    private string _footer = string.Empty;

    public WholesaleBillViewModel()
    {
        _purchaseService = PurchaseService.Instance;
        Title = "Wholesaler Bills";
    }

    public ObservableCollection<PurchaseSummaryItem> Bills { get; } = new();

    public string Footer
    {
        get => _footer;
        set => SetProperty(ref _footer, value);
    }

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            Bills.Clear();
            var items = await _purchaseService.GetPurchasesAsync();
            foreach (var item in items)
                Bills.Add(item);

            Footer = Bills.Count == 0
                ? "No supplier bills yet. Add a bill to bring stock in."
                : $"{Bills.Count} {(Bills.Count == 1 ? "bill" : "bills")}";
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to load bills: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task NavigateToNewAsync()
    {
        await NavigationGuard.GoToAsync("WholesaleBillEdit");
    }

    public async Task OpenAsync(PurchaseSummaryItem item)
    {
        if (item != null)
            await NavigationGuard.GoToAsync($"WholesaleBillEdit?id={item.Id}");
    }
}