using DigiPasal.Models;
using SQLite;

namespace DigiPasal.Services;

public class PurchaseService
{
    private static readonly Lazy<PurchaseService> _lazyInstance = new(() => new PurchaseService());

    public static PurchaseService Instance => _lazyInstance.Value;

    private readonly DatabaseService _dbService;

    private PurchaseService()
    {
        _dbService = DatabaseService.Instance;
    }

    private SQLiteAsyncConnection Database => _dbService.Database;

    public async Task<List<PurchaseSummaryItem>> GetPurchasesAsync()
    {
        var raw = await Database.QueryAsync<PurchaseSummaryRow>(
            "SELECT p.Id, p.BillNumber, p.WholesalerName, p.BillDate, p.TotalAmount, p.Notes, p.CreatedAt, " +
            "(SELECT COUNT(*) FROM PurchaseItems i WHERE i.PurchaseId = p.Id) AS ItemCount " +
            "FROM Purchases p ORDER BY p.BillDate DESC, p.Id DESC");

        return raw.Select(r => new PurchaseSummaryItem(r)).ToList();
    }

    public async Task<Purchase?> GetPurchaseAsync(int id)
    {
        return await Database.Table<Purchase>()
            .Where(p => p.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<List<PurchaseItem>> GetPurchaseItemsAsync(int purchaseId)
    {
        return await Database.Table<PurchaseItem>()
            .Where(i => i.PurchaseId == purchaseId)
            .OrderBy(i => i.Id)
            .ToListAsync();
    }

    public async Task<PurchaseBill> GetPurchaseBillAsync(int id)
    {
        var purchase = await GetPurchaseAsync(id) ?? throw new InvalidOperationException("Bill not found.");
        var items = await GetPurchaseItemsAsync(id);
        return new PurchaseBill(purchase, items);
    }

    public async Task<int> SavePurchaseAsync(Purchase purchase, List<PurchaseItem> items)
    {
        if (purchase == null)
            throw new ArgumentNullException(nameof(purchase));

        purchase.BillNumber = purchase.BillNumber?.Trim() ?? string.Empty;
        purchase.WholesalerName = purchase.WholesalerName?.Trim() ?? string.Empty;

        var validItems = items.Where(i => i.Quantity > 0).ToList();

        await Database.RunInTransactionAsync(tran =>
        {
            bool isNew = purchase.Id == 0;
            if (isNew)
            {
                purchase.CreatedAt = DateTime.UtcNow;
                tran.Insert(purchase);
            }
            else
            {
                tran.Update(purchase);
            }

            if (!isNew)
            {
                foreach (var old in tran.Table<PurchaseItem>().Where(i => i.PurchaseId == purchase.Id).ToList())
                {
                    RevertItemStock(tran, old);
                    tran.Delete(old);
                }
            }

            foreach (var item in validItems)
            {
                item.PurchaseId = purchase.Id;
                item.ProductName = item.ProductName?.Trim() ?? string.Empty;
                item.LineTotal = (decimal)item.Quantity * item.CostPrice;
                tran.Insert(item);
                ApplyItemStock(tran, item);
            }
        });

        return purchase.Id;
    }

    public async Task DeletePurchaseAsync(int id)
    {
        await Database.RunInTransactionAsync(tran =>
        {
            foreach (var item in tran.Table<PurchaseItem>().Where(i => i.PurchaseId == id).ToList())
            {
                RevertItemStock(tran, item);
                tran.Delete(item);
            }

            tran.Execute("DELETE FROM Purchases WHERE Id = ?", id);
        });
    }

    private static void ApplyItemStock(SQLiteConnection tran, PurchaseItem item)
    {
        var product = tran.Table<Product>()
            .Where(p => p.Id == item.ProductId)
            .FirstOrDefault();
        if (product == null)
            return;

        product.StockQuantity += item.Quantity;
        if (item.CostPrice > 0)
            product.CostPrice = item.CostPrice;
        product.UpdatedAt = DateTime.UtcNow;
        tran.Update(product);
    }

    private static void RevertItemStock(SQLiteConnection tran, PurchaseItem item)
    {
        var product = tran.Table<Product>()
            .Where(p => p.Id == item.ProductId)
            .FirstOrDefault();
        if (product == null)
            return;

        product.StockQuantity -= item.Quantity;
        if (product.StockQuantity < 0)
            product.StockQuantity = 0;
        product.UpdatedAt = DateTime.UtcNow;
        tran.Update(product);
    }
}

public class PurchaseSummaryRow
{
    public int Id { get; set; }
    public string BillNumber { get; set; } = string.Empty;
    public string WholesalerName { get; set; } = string.Empty;
    public DateTime BillDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int ItemCount { get; set; }
}

public class PurchaseSummaryItem
{
    public PurchaseSummaryItem(PurchaseSummaryRow row)
    {
        Id = row.Id;
        BillNumber = row.BillNumber;
        WholesalerName = row.WholesalerName;
        BillDate = row.BillDate;
        TotalAmount = row.TotalAmount;
        Notes = row.Notes;
        CreatedAt = row.CreatedAt;
        ItemCount = row.ItemCount;
    }

    public int Id { get; }
    public string BillNumber { get; }
    public string WholesalerName { get; }
    public DateTime BillDate { get; }
    public decimal TotalAmount { get; }
    public string Notes { get; }
    public DateTime CreatedAt { get; }
    public int ItemCount { get; }

    public string TitleDisplay => string.IsNullOrWhiteSpace(BillNumber) ? "Unnumbered bill" : $"Bill #{BillNumber}";
    public string DateDisplay => NepaliDateConverter.Format(BillDate);
    public string DateShort => NepaliDateConverter.FormatShort(BillDate);
    public string TotalDisplay => CurrencyFormatter.Format(TotalAmount);
    public string ItemsDisplay => $"{ItemCount} {(ItemCount == 1 ? "item" : "items")}";
}

public class PurchaseBill
{
    public PurchaseBill(Purchase purchase, List<PurchaseItem> items)
    {
        Purchase = purchase;
        Items = items;
    }

    public Purchase Purchase { get; }
    public List<PurchaseItem> Items { get; }

    public string TitleDisplay => string.IsNullOrWhiteSpace(Purchase.BillNumber) ? "Unnumbered bill" : $"Bill #{Purchase.BillNumber}";
    public decimal Total => Items.Sum(i => i.LineTotal);
    public string TotalDisplay => CurrencyFormatter.Format(Total);
    public string DateDisplay => NepaliDateConverter.Format(Purchase.BillDate);
    public string DateShort => NepaliDateConverter.FormatShort(Purchase.BillDate);
    public string WholesalerDisplay => string.IsNullOrWhiteSpace(Purchase.WholesalerName) ? "—" : Purchase.WholesalerName;
    public string ItemsDisplay => $"{Items.Count} {(Items.Count == 1 ? "item" : "items")}";
}