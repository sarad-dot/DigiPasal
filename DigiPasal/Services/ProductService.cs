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
        var products = await Database.Table<Product>()
            .Where(p => p.IsActive)
            .ToListAsync();

        return products
            .Select(p => p.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();
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