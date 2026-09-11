using System.Collections.ObjectModel;
using System.Windows.Input;
using DigiPasal.Models;
using DigiPasal.Services;

namespace DigiPasal.ViewModels;

public class AddEditProductViewModel : BaseViewModel, IQueryAttributable
{
    private readonly ProductService _productService;
    private bool _isSaving;

    private int _productId;
    private string _name = string.Empty;
    private string _category = string.Empty;
    private string _description = string.Empty;
    private string _sellingPrice = string.Empty;
    private string _costPrice = string.Empty;
    private string _unit = "pcs";
    private string _stockQuantity = string.Empty;
    private string _lowStockThreshold = "10";
    private string _barcode = string.Empty;
    private string _sku = string.Empty;
    private bool _isActive = true;

    public int ProductId
    {
        get => _productId;
        set => SetProperty(ref _productId, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string SellingPrice
    {
        get => _sellingPrice;
        set => SetProperty(ref _sellingPrice, value);
    }

    public string CostPrice
    {
        get => _costPrice;
        set => SetProperty(ref _costPrice, value);
    }

    public string Unit
    {
        get => _unit;
        set => SetProperty(ref _unit, value);
    }

    public string StockQuantity
    {
        get => _stockQuantity;
        set => SetProperty(ref _stockQuantity, value);
    }

    public string LowStockThreshold
    {
        get => _lowStockThreshold;
        set => SetProperty(ref _lowStockThreshold, value);
    }

    public string Barcode
    {
        get => _barcode;
        set => SetProperty(ref _barcode, value);
    }

    public string SKU
    {
        get => _sku;
        set => SetProperty(ref _sku, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public bool IsEditMode => ProductId > 0;

    public ObservableCollection<string> Units { get; } = new()
    {
        "pcs", "kg", "ltr", "m", "dozen", "box"
    };

    public ObservableCollection<string> Categories { get; } = new()
    {
        "Groceries", "Beverages", "Snacks", "Dairy",
        "Personal Care", "Household", "Stationery", "Other"
    };

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ScanBarcodeCommand { get; }

    public AddEditProductViewModel()
    {
        _productService = ProductService.Instance;

        SaveCommand = new Command(async () => await SaveAsync());
        CancelCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
        ScanBarcodeCommand = new Command(async () =>
        {
            await Shell.Current.DisplayAlertAsync(
                "Coming Soon",
                "Barcode scanning will be available in a future update.",
                "OK");
        });
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var idObj) && idObj is string idStr && int.TryParse(idStr, out var id))
        {
            ProductId = id;
            Title = "Edit Product";
            _ = LoadProductAsync(id);
        }
        else
        {
            Title = "Add Product";
        }
    }

    private async Task LoadProductAsync(int id)
    {
        var product = await _productService.GetProductAsync(id);
        if (product == null)
        {
            await Shell.Current.DisplayAlertAsync(
                "Product Not Found",
                "This product no longer exists. It may have been deleted.",
                "OK");
            await Shell.Current.GoToAsync("..");
            return;
        }

        Name = product.Name;
        Category = product.Category;
        Description = product.Description;
        SellingPrice = product.SellingPrice.ToString();
        CostPrice = product.CostPrice.ToString();
        Unit = product.Unit;
        StockQuantity = product.StockQuantity.ToString();
        LowStockThreshold = product.LowStockThreshold.ToString();
        Barcode = product.Barcode;
        SKU = product.SKU;
        IsActive = product.IsActive;
    }

    private async Task SaveAsync()
    {
        if (_isSaving)
            return;

        if (string.IsNullOrWhiteSpace(Name))
        {
            await Shell.Current.DisplayAlertAsync("Validation", "Product name is required.", "OK");
            return;
        }

        if (!decimal.TryParse(SellingPrice, out var sellPrice) || sellPrice < 0)
        {
            await Shell.Current.DisplayAlertAsync("Validation", "Selling price must be a valid positive number.", "OK");
            return;
        }

        decimal.TryParse(CostPrice, out var costPrice);
        double.TryParse(StockQuantity, out var stockQty);
        double.TryParse(LowStockThreshold, out var threshold);

        var product = new Product
        {
            Id = ProductId,
            Name = Name.Trim(),
            Category = Category,
            Description = Description,
            SellingPrice = sellPrice,
            CostPrice = costPrice < 0 ? 0 : costPrice,
            Unit = Unit,
            StockQuantity = stockQty < 0 ? 0 : stockQty,
            LowStockThreshold = threshold < 0 ? 0 : threshold,
            Barcode = Barcode?.Trim() ?? string.Empty,
            SKU = SKU?.Trim() ?? string.Empty,
            IsActive = IsActive
        };

        try
        {
            _isSaving = true;

            await _productService.SaveProductAsync(product);
            await Shell.Current.DisplayAlertAsync(
                "Success",
                IsEditMode ? "Product updated successfully." : "Product added successfully.",
                "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to save product: {ex.Message}", "OK");
        }
        finally
        {
            _isSaving = false;
        }
    }
}
