using System.Collections.ObjectModel;
using System.Windows.Input;
using DigiPasal.Models;
using DigiPasal.Services;

namespace DigiPasal.ViewModels;

public class SaleViewModel : BaseViewModel
{
    private readonly ProductService _productService;
    private readonly CartState _cartState;

    private ObservableCollection<Product> _products = new();
    private ObservableCollection<Product> _filteredProducts = new();
    private string _searchQuery = string.Empty;
    private string _selectedCategory = "All";
    private ObservableCollection<string> _categories = new();
    private int _cartCount;
    private bool _initialized;

    private bool _isQuantityPopupVisible;
    private Product? _popupProduct;
    private double _popupQuantity = 1;
    private string _popupQuantityText = "1";

    public ObservableCollection<Product> Products
    {
        get => _products;
        set => SetProperty(ref _products, value);
    }

    public ObservableCollection<Product> FilteredProducts
    {
        get => _filteredProducts;
        set => SetProperty(ref _filteredProducts, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
                _ = ApplyFilterAsync();
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
                _ = ApplyFilterAsync();
        }
    }

    public ObservableCollection<string> Categories
    {
        get => _categories;
        set => SetProperty(ref _categories, value);
    }

    public int CartCount
    {
        get => _cartCount;
        set => SetProperty(ref _cartCount, value);
    }

    public bool IsQuantityPopupVisible
    {
        get => _isQuantityPopupVisible;
        set => SetProperty(ref _isQuantityPopupVisible, value);
    }

    public Product? PopupProduct
    {
        get => _popupProduct;
        set => SetProperty(ref _popupProduct, value);
    }

    public double PopupQuantity
    {
        get => _popupQuantity;
        set
        {
            if (SetProperty(ref _popupQuantity, value))
                PopupQuantityText = value > 0 ? value.ToString("0.##") : "1";
        }
    }

    public string PopupQuantityText
    {
        get => _popupQuantityText;
        set
        {
            if (SetProperty(ref _popupQuantityText, value))
            {
                if (double.TryParse(value, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var qty) && qty > 0)
                {
                    _popupQuantity = qty;
                    OnPropertyChanged(nameof(PopupQuantity));
                    OnPropertyChanged(nameof(PopupStockHint));
                }
            }
        }
    }

    public string PopupStockHint => PopupProduct != null
        ? $"Available: {PopupProduct.StockQuantity:0.##}"
        : "";

    public ICommand LoadProductsCommand { get; }
    public ICommand AddToCartCommand { get; }
    public ICommand SelectCategoryCommand { get; }
    public ICommand GoToCartCommand { get; }
    public ICommand ScanBarcodeCommand { get; }
    public ICommand ShowQuantityPopupCommand { get; }
    public ICommand ConfirmAddToCartCommand { get; }
    public ICommand CancelQuantityPopupCommand { get; }
    public ICommand SetQuickQuantityCommand { get; }

    public SaleViewModel()
    {
        _productService = ProductService.Instance;
        _cartState = CartState.Instance;
        Title = "New Sale";

        _cartState.CartChanged += OnCartChanged;

        LoadProductsCommand = new Command(async () => await LoadProductsAsync());
        AddToCartCommand = new Command<Product>(ShowQuantityPopup);
        SelectCategoryCommand = new Command<string>(cat => SelectedCategory = cat ?? "All");
        GoToCartCommand = new Command(async () => await Shell.Current.GoToAsync("Cart"));
        ScanBarcodeCommand = new Command(async () =>
        {
            await Shell.Current.DisplayAlertAsync(
                "Coming Soon",
                "Barcode scanning will be available in a future update. You can type the barcode manually in the search field.",
                "OK");
        });
        ShowQuantityPopupCommand = new Command<Product>(ShowQuantityPopup);
        ConfirmAddToCartCommand = new Command(ConfirmAddToCart);
        CancelQuantityPopupCommand = new Command(CancelQuantityPopup);
        SetQuickQuantityCommand = new Command<double>(SetQuickQuantity);
    }

    private void OnCartChanged()
    {
        CartCount = _cartState.ItemCount;
    }

    public void Cleanup()
    {
        _cartState.CartChanged -= OnCartChanged;
    }

    public async Task LoadProductsAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var products = await _productService.GetActiveProductsAsync();
            Products = new ObservableCollection<Product>(products);

            if (!_initialized)
            {
                var categories = await _productService.GetCategoriesAsync();
                categories.Insert(0, "All");
                Categories = new ObservableCollection<string>(categories);
                SelectedCategory = "All";
                _initialized = true;
            }

            await ApplyFilterAsync();

            CartCount = _cartState.ItemCount;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ApplyFilterAsync()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchQuery)
            ? new List<Product>(Products)
            : Products.Where(p =>
                (p.Name ?? string.Empty).Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                (p.Barcode ?? string.Empty).Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                (p.SKU ?? string.Empty).Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                .ToList();

        if (!string.IsNullOrWhiteSpace(SelectedCategory) &&
            !string.Equals(SelectedCategory, "All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(p =>
                string.Equals(p.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        FilteredProducts = new ObservableCollection<Product>(
            filtered.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase));
    }

    private void ShowQuantityPopup(Product? product)
    {
        if (product == null) return;

        if (product.StockQuantity <= 0)
        {
            Shell.Current.DisplayAlertAsync("Out of Stock", $"\"{product.Name}\" is out of stock.", "OK");
            return;
        }

        PopupProduct = product;
        PopupQuantity = 1;
        IsQuantityPopupVisible = true;
    }

    private void ConfirmAddToCart()
    {
        if (PopupProduct == null) return;

        if (PopupQuantity <= 0) PopupQuantity = 1;

        var stockAvailable = PopupProduct.StockQuantity;
        var existing = _cartState.Items.FirstOrDefault(c => c.ProductId == PopupProduct.Id);
        if (existing != null)
            stockAvailable -= existing.Quantity;

        if (stockAvailable <= 0)
        {
            Shell.Current.DisplayAlertAsync("Stock Limit",
                $"No more stock available for \"{PopupProduct.Name}\".", "OK");
            CancelQuantityPopup();
            return;
        }

        if (PopupQuantity > stockAvailable)
            PopupQuantity = stockAvailable;

        _cartState.AddProduct(PopupProduct, PopupQuantity);
        CancelQuantityPopup();
    }

    private void CancelQuantityPopup()
    {
        IsQuantityPopupVisible = false;
        PopupProduct = null;
        PopupQuantity = 1;
    }

    private void SetQuickQuantity(double qty)
    {
        if (PopupProduct != null && qty > PopupProduct.StockQuantity)
            qty = PopupProduct.StockQuantity;

        PopupQuantity = qty;
    }
}
