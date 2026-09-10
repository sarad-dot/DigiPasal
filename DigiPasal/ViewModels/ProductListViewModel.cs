using System.Collections.ObjectModel;
using System.Windows.Input;
using DigiPasal.Models;
using DigiPasal.Services;

namespace DigiPasal.ViewModels;

public class ProductListViewModel : BaseViewModel
{
    private readonly ProductService _productService;

    private ObservableCollection<Product> _products = new();
    private ObservableCollection<Product> _filteredProducts = new();
    private ObservableCollection<string> _categories = new();
    private string _searchQuery = string.Empty;
    private string _selectedCategory = "All";
    private bool _isSearchVisible;
    private readonly Debouncer _filterDebouncer = new();

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

    public ObservableCollection<string> Categories
    {
        get => _categories;
        set => SetProperty(ref _categories, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
                _filterDebouncer.Debounce(250, ApplyFilterAsync);
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
                _filterDebouncer.Debounce(250, ApplyFilterAsync);
        }
    }

    public bool IsSearchVisible
    {
        get => _isSearchVisible;
        set => SetProperty(ref _isSearchVisible, value);
    }

    public ICommand LoadProductsCommand { get; }
    public ICommand AddProductCommand { get; }
    public ICommand EditProductCommand { get; }
    public ICommand DeleteProductCommand { get; }
    public ICommand ToggleSearchCommand { get; }
    public ICommand SelectCategoryCommand { get; }

    public ProductListViewModel()
    {
        _productService = ProductService.Instance;
        Title = "Products";

        LoadProductsCommand = new Command(async () => await LoadProductsAsync());
        AddProductCommand = new Command(async () => await Shell.Current.GoToAsync("AddProduct"));
        EditProductCommand = new Command<Product>(async (product) => await EditProductAsync(product));
        DeleteProductCommand = new Command<Product>(async (product) => await DeleteProductAsync(product));
        ToggleSearchCommand = new Command(() => IsSearchVisible = !IsSearchVisible);
        SelectCategoryCommand = new Command<string>(cat => SelectedCategory = cat ?? "All");
    }

    public async Task LoadProductsAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            var products = await _productService.GetAllProductsAsync();
            Products = new ObservableCollection<Product>(products);

            var categories = await _productService.GetCategoriesAsync();
            categories.Insert(0, "All");
            Categories = new ObservableCollection<string>(categories);

            SelectedCategory = "All";
            await ApplyFilterAsync();
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
                p.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                p.Barcode.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                p.SKU.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
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

    private async Task EditProductAsync(Product? product)
    {
        if (product == null)
            return;

        await Shell.Current.GoToAsync($"EditProduct?id={product.Id}");
    }

    private async Task DeleteProductAsync(Product? product)
    {
        if (product == null)
            return;

        var confirmed = await Shell.Current.DisplayAlertAsync(
            "Delete Product",
            $"Are you sure you want to delete \"{product.Name}\"?",
            "Delete",
            "Cancel");

        if (!confirmed)
            return;

        var deleted = await _productService.DeleteProductAsync(product.Id);
        if (deleted)
        {
            Products.Remove(product);
            await ApplyFilterAsync();
        }
    }
}
