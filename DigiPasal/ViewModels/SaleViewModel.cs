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

    public ICommand LoadProductsCommand { get; }
    public ICommand AddToCartCommand { get; }
    public ICommand SelectCategoryCommand { get; }
    public ICommand GoToCartCommand { get; }
    public ICommand ScanBarcodeCommand { get; }

    public SaleViewModel()
    {
        _productService = ProductService.Instance;
        _cartState = CartState.Instance;
        Title = "New Sale";

        _cartState.CartChanged += OnCartChanged;

        LoadProductsCommand = new Command(async () => await LoadProductsAsync());
        AddToCartCommand = new Command<Product>(async (product) => await AddToCartAsync(product));
        SelectCategoryCommand = new Command<string>(cat => SelectedCategory = cat ?? "All");
        GoToCartCommand = new Command(async () => await Shell.Current.GoToAsync("Cart"));
        ScanBarcodeCommand = new Command(async () =>
        {
            await Shell.Current.DisplayAlert(
                "Coming Soon",
                "Barcode scanning will be available in a future update. You can type the barcode manually in the search field.",
                "OK");
        });
    }

    private void OnCartChanged()
    {
        CartCount = _cartState.ItemCount;
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

            var categories = await _productService.GetCategoriesAsync();
            categories.Insert(0, "All");
            Categories = new ObservableCollection<string>(categories);

            SelectedCategory = "All";
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

    private async Task AddToCartAsync(Product? product)
    {
        if (product == null)
            return;

        if (product.StockQuantity <= 0)
        {
            await Shell.Current.DisplayAlert("Out of Stock", $"\"{product.Name}\" is out of stock.", "OK");
            return;
        }

        _cartState.AddProduct(product);
        await Shell.Current.DisplayAlert("Added", $"{product.Name} added to cart.", "OK");
    }
}
