using DigiPasal.Models;
using SQLite;

namespace DigiPasal.Services;

public class ProductService
{
    private static readonly Lazy<ProductService> _lazyInstance = new(() => new ProductService());

    public static ProductService Instance => _lazyInstance.Value;

    private readonly DatabaseService _dbService;

    private ProductService()
    {
        _dbService = DatabaseService.Instance;
    }

    private SQLiteAsyncConnection Database => _dbService.Database;

    public async Task<List<Product>> GetActiveProductsAsync(string? query = null, string? category = null)
    {
        var products = await Database.Table<Product>()
            .Where(p => p.IsActive)
            .ToListAsync();

        return Filter(products, query, category);
    }

    public async Task<List<Product>> GetAllProductsAsync(string? query = null)
    {
        var products = await Database.Table<Product>().ToListAsync();

        return Filter(products, query, null);
    }

    public async Task<List<string>> GetCategoriesAsync()
    {
        return await Database.QueryScalarsAsync<string>(
            "SELECT DISTINCT Category FROM Products " +
            "WHERE IsActive = 1 AND Category IS NOT NULL AND Category <> '' " +
            "ORDER BY Category COLLATE NOCASE");
    }

    public async Task<Product?> GetProductAsync(int id)
    {
        return await Database.Table<Product>()
            .Where(p => p.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<int> SaveProductAsync(Product product)
    {
        if (product == null)
            throw new ArgumentNullException(nameof(product));

        var trimmedName = product.Name?.Trim() ?? string.Empty;
        product.Name = trimmedName;
        product.Category = product.Category?.Trim() ?? string.Empty;
        product.Barcode = product.Barcode?.Trim() ?? string.Empty;
        product.Unit = string.IsNullOrWhiteSpace(product.Unit) ? "pcs" : product.Unit;

        if (product.SellingPrice < 0)
            product.SellingPrice = 0;
        if (product.CostPrice < 0)
            product.CostPrice = 0;
        if (product.StockQuantity < 0)
            product.StockQuantity = 0;
        if (product.LowStockThreshold < 0)
            product.LowStockThreshold = 0;

        if (product.Id == 0)
        {
            product.CreatedAt = DateTime.UtcNow;
            product.UpdatedAt = product.CreatedAt;
            await Database.InsertAsync(product);
        }
        else
        {
            product.UpdatedAt = DateTime.UtcNow;
            await Database.UpdateAsync(product);
        }

        return product.Id;
    }

    public async Task<bool> DeleteProductAsync(int id)
    {
        int deleted = await Database.DeleteAsync<Product>(id);
        return deleted > 0;
    }

    public async Task<Product?> GetProductByBarcodeAsync(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return null;

        return await Database.Table<Product>()
            .Where(p => p.IsActive && p.Barcode == barcode.Trim())
            .FirstOrDefaultAsync();
    }

    public async Task UpdateStockAsync(int productId, double quantityChange)
    {
        var product = await GetProductAsync(productId);
        if (product == null)
            return;

        product.StockQuantity += quantityChange;
        if (product.StockQuantity < 0)
            product.StockQuantity = 0;
        product.UpdatedAt = DateTime.UtcNow;
        await Database.UpdateAsync(product);
    }

    public async Task<List<Product>> GetLowStockProductsAsync()
    {
        return await Database.Table<Product>()
            .Where(p => p.IsActive && p.StockQuantity <= p.LowStockThreshold)
            .OrderBy(p => p.StockQuantity)
            .ToListAsync();
    }

    public async Task<List<StockRow>> GetStockValuationAsync(string? category = null)
    {
        var rows = await Database.QueryAsync<StockRow>(
            "SELECT Id, Name, Category, Unit, StockQuantity, LowStockThreshold, " +
            "SellingPrice, CostPrice, " +
            "(StockQuantity * CostPrice) AS CostValue, " +
            "(StockQuantity * SellingPrice) AS SellValue " +
            "FROM Products WHERE IsActive = 1 " +
            (string.IsNullOrWhiteSpace(category) || category == "All"
                ? ""
                : $"AND Category = ? ") +
            "ORDER BY Name COLLATE NOCASE", category);

        foreach (var row in rows)
        {
            row.IsLowStock = row.StockQuantity <= row.LowStockThreshold;
            row.DisplayQuantity = $"{row.StockQuantity:N0} {row.Unit}";
        }

        return rows;
    }

    private static List<Product> Filter(List<Product> products, string? query, string? category)
    {
        if (products == null || products.Count == 0)
            return new List<Product>();

        if (!string.IsNullOrWhiteSpace(category) && !string.Equals(category, "All", StringComparison.OrdinalIgnoreCase))
        {
            products = products
                .Where(p => string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var q = query?.Trim() ?? string.Empty;
        if (q.Length > 0)
        {
            q = q.ToLowerInvariant();
            products = products
                .Where(p =>
                    (p.Name ?? string.Empty).ToLowerInvariant().Contains(q) ||
                    (p.Barcode ?? string.Empty).ToLowerInvariant().Contains(q))
                .ToList();
        }

        return products
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

public class StockRow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Unit { get; set; } = "pcs";
    public double StockQuantity { get; set; }
    public double LowStockThreshold { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal CostPrice { get; set; }
    public decimal CostValue { get; set; }
    public decimal SellValue { get; set; }
    public bool IsLowStock { get; set; }
    public string DisplayQuantity { get; set; } = string.Empty;
}