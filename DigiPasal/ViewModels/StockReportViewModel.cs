using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using DigiPasal.Services;

namespace DigiPasal.ViewModels;

public class StockReportViewModel : BaseViewModel
{
    private readonly ProductService _productService;

    private string _selectedCategory = "All";
    private decimal _totalCostValue;
    private decimal _totalSellValue;
    private int _lowStockCount;
    private int _productCount;
    private bool _isExporting;

    public ObservableCollection<StockRow> Rows { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();

    public StockReportViewModel()
    {
        _productService = ProductService.Instance;
        Title = "Stock Report";

        LoadCommand = new Command(async () => await LoadAsync());
        ExportCommand = new Command(async () => await ExportAsync());
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }

    public decimal TotalCostValue
    {
        get => _totalCostValue;
        set { SetProperty(ref _totalCostValue, value); OnPropertyChanged(nameof(TotalCostValueDisplay)); }
    }

    public decimal TotalSellValue
    {
        get => _totalSellValue;
        set { SetProperty(ref _totalSellValue, value); OnPropertyChanged(nameof(TotalSellValueDisplay)); }
    }

    public int LowStockCount
    {
        get => _lowStockCount;
        set { SetProperty(ref _lowStockCount, value); OnPropertyChanged(nameof(LowStockCountDisplay)); }
    }

    public int ProductCount
    {
        get => _productCount;
        set { SetProperty(ref _productCount, value); OnPropertyChanged(nameof(ProductCountDisplay)); }
    }

    public string TotalCostValueDisplay => CurrencyFormatter.Format(TotalCostValue);
    public string TotalSellValueDisplay => CurrencyFormatter.Format(TotalSellValue);
    public string LowStockCountDisplay => $"{LowStockCount} low stock";
    public string ProductCountDisplay => $"{ProductCount} products";

    public bool IsExporting
    {
        get => _isExporting;
        set => SetProperty(ref _isExporting, value);
    }

    public ICommand LoadCommand { get; }
    public ICommand ExportCommand { get; }

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var categories = await _productService.GetCategoriesAsync();
            Categories.Clear();
            Categories.Add("All");
            foreach (var cat in categories)
                Categories.Add(cat);

            var rows = await _productService.GetStockValuationAsync(SelectedCategory);

            TotalCostValue = rows.Sum(r => r.CostValue);
            TotalSellValue = rows.Sum(r => r.SellValue);
            LowStockCount = rows.Count(r => r.IsLowStock);
            ProductCount = rows.Count;

            Rows.Clear();
            foreach (var row in rows)
                Rows.Add(row);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to load stock report: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExportAsync()
    {
        if (IsExporting)
            return;

        IsExporting = true;
        try
        {
            var rows = new List<string[]>(Rows.Count);
            foreach (var r in Rows)
                rows.Add(new[]
                {
                    r.Name,
                    r.Category,
                    r.DisplayQuantity,
                    CreditReportExportService.FormatN(r.CostPrice),
                    CreditReportExportService.FormatN(r.SellingPrice),
                    CreditReportExportService.FormatN(r.CostValue),
                    CreditReportExportService.FormatN(r.SellValue),
                    r.IsLowStock ? "Yes" : "No"
                });

            if (rows.Count == 0)
            {
                await Shell.Current.DisplayAlertAsync("Export", "No stock data to export.", "OK");
                return;
            }

            var category = string.Equals(SelectedCategory, "All", StringComparison.OrdinalIgnoreCase)
                ? "all"
                : SelectedCategory.ToLowerInvariant().Replace(' ', '_');

            await CreditReportExportService.ExportAsync(
                "Stock Report",
                $"stock_report_{category}_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                new[] { "Product", "Category", "Quantity", "Cost Price", "Selling Price", "Value (Cost)", "Value (Sell)", "Low Stock" },
                rows);
        }
        finally
        {
            IsExporting = false;
        }
    }
}