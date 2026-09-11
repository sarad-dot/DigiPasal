using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using DigiPasal.Models;
using DigiPasal.Services;

namespace DigiPasal.ViewModels;

public class WholesaleBillEditViewModel : BaseViewModel, IQueryAttributable
{
    private readonly PurchaseService _purchaseService;
    private readonly ProductService _productService;

    private int _billId;
    private string _billNumber = string.Empty;
    private string _wholesalerName = string.Empty;
    private DateTime _billDate = DateTime.Today;
    private string _notes = string.Empty;
    private decimal _total;
    private bool _isExisting;
    private bool _isSaving;

    public WholesaleBillEditViewModel()
    {
        _purchaseService = PurchaseService.Instance;
        _productService = ProductService.Instance;
        Title = "Supplier Bill";

        AddItemCommand = new Command(() => AddItem());
        RemoveItemCommand = new Command<BillItemEdit>(RemoveItem);
        SaveCommand = new Command(async () => await SaveAsync());
    }

    public ICommand AddItemCommand { get; }
    public ICommand RemoveItemCommand { get; }
    public ICommand SaveCommand { get; }

    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<BillItemEdit> Items { get; } = new();

    public string BillNumber
    {
        get => _billNumber;
        set => SetProperty(ref _billNumber, value);
    }

    public string WholesalerName
    {
        get => _wholesalerName;
        set => SetProperty(ref _wholesalerName, value);
    }

    public DateTime BillDate
    {
        get => _billDate;
        set
        {
            if (SetProperty(ref _billDate, value.Date))
                OnPropertyChanged(nameof(DateNepaliDisplay));
        }
    }

    public DateTime MaximumDate => DateTime.Today;

    public string DateNepaliDisplay => NepaliDateConverter.Format(BillDate);

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public decimal Total
    {
        get => _total;
        set
        {
            if (SetProperty(ref _total, value))
                OnPropertyChanged(nameof(TotalDisplay));
        }
    }

    public string TotalDisplay => CurrencyFormatter.Format(Total);

    public bool IsExisting
    {
        get => _isExisting;
        set => SetProperty(ref _isExisting, value);
    }

    public bool ItemsExist => Items.Count > 0;

    public void ApplyQueryAttributes(IDictionary<string, object?> query)
    {
        if (query.TryGetValue("id", out var value) && int.TryParse(value?.ToString(), out var id) && id > 0)
        {
            _billId = id;
            Title = "Edit Supplier Bill";
        }
    }

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var products = await _productService.GetActiveProductsAsync();
            Products.Clear();
            foreach (var p in products)
                Products.Add(p);

            Items.CollectionChanged += Items_CollectionChanged;

            if (_billId > 0)
            {
                var bill = await _purchaseService.GetPurchaseBillAsync(_billId);
                BillNumber = bill.Purchase.BillNumber;
                WholesalerName = bill.Purchase.WholesalerName;
                BillDate = bill.Purchase.BillDate;
                Notes = bill.Purchase.Notes;
                IsExisting = true;

                foreach (var it in bill.Items)
                {
                    var product = Products.FirstOrDefault(p => p.Id == it.ProductId);
                    var edit = new BillItemEdit(Products)
                    {
                        SelectedProduct = product,
                        QuantityText = it.Quantity.ToString("0.##"),
                        CostText = it.CostPrice.ToString("0.##")
                    };
                    Items.Add(edit);
                }
            }
            else
            {
                AddItem();
            }

            RecomputeTotal();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to load: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Items_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (object item in e.NewItems)
                if (item is BillItemEdit edit)
                    edit.PropertyChanged += OnItemPropertyChanged;

        if (e.OldItems != null)
            foreach (object item in e.OldItems)
                if (item is BillItemEdit edit)
                    edit.PropertyChanged -= OnItemPropertyChanged;

        OnPropertyChanged(nameof(ItemsExist));
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BillItemEdit.LineTotal))
            RecomputeTotal();
    }

    private void AddItem()
    {
        Items.Add(new BillItemEdit(Products) { QuantityText = "1" });
    }

    private void RemoveItem(BillItemEdit? item)
    {
        if (item != null && Items.Remove(item))
            RecomputeTotal();
    }

    private void RecomputeTotal()
    {
        Total = Items.Sum(i => i.LineTotal);
        OnPropertyChanged(nameof(ItemsExist));
    }

    private async Task SaveAsync()
    {
        if (_isSaving)
            return;

        var valid = Items.Where(i => i.SelectedProduct != null && i.Quantity > 0).ToList();
        if (valid.Count == 0)
        {
            await Shell.Current.DisplayAlertAsync("Supplier Bill", "Add at least one product line with a quantity.", "OK");
            return;
        }

        var purchase = new Purchase
        {
            Id = _billId,
            BillNumber = BillNumber,
            WholesalerName = WholesalerName,
            BillDate = BillDate,
            TotalAmount = valid.Sum(i => i.LineTotal),
            Notes = Notes,
            CreatedAt = DateTime.UtcNow
        };

        var items = valid.Select(i => new PurchaseItem
        {
            PurchaseId = purchase.Id,
            ProductId = i.SelectedProduct!.Id,
            ProductName = i.SelectedProduct.Name,
            Quantity = i.Quantity,
            CostPrice = i.CostPrice,
            LineTotal = i.LineTotal
        }).ToList();

        try
        {
            _isSaving = true;

            await _purchaseService.SavePurchaseAsync(purchase, items);
            await Shell.Current.DisplayAlertAsync(
                IsExisting ? "Bill Updated" : "Bill Added",
                $"Stock increased by {valid.Count} {(valid.Count == 1 ? "line" : "lines")}.",
                "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to save bill: {ex.Message}", "OK");
        }
        finally
        {
            _isSaving = false;
        }
    }
}