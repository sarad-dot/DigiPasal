using DigiPasal.Models;
using SQLite;

namespace DigiPasal.Services;

public class SaleService
{
    private static readonly Lazy<SaleService> _lazyInstance = new(() => new SaleService());

    public static SaleService Instance => _lazyInstance.Value;

    private readonly DatabaseService _dbService;
    private static readonly object _receiptLock = new();

    private SaleService()
    {
        _dbService = DatabaseService.Instance;
    }

    private SQLiteAsyncConnection Database => _dbService.Database;

    public async Task<Sale> CreateSaleAsync(Sale sale, List<SaleItem> items)
    {
        if (sale == null)
            throw new ArgumentNullException(nameof(sale));
        if (items == null || items.Count == 0)
            throw new ArgumentException("Sale must have at least one item.", nameof(items));

        sale.ReceiptNumber = GenerateReceiptNumber();
        sale.CreatedAt = DateTime.UtcNow;

        var copiedItems = items.Select(i => new SaleItem
        {
            ProductId = i.ProductId,
            ProductName = i.ProductName ?? string.Empty,
            Unit = i.Unit ?? "pcs",
            UnitPrice = i.UnitPrice,
            Quantity = i.Quantity,
            Discount = i.Discount,
            DiscountType = i.DiscountType ?? "None",
            LineTotal = i.LineTotal
        }).ToList();

        await Database.RunInTransactionAsync(tran =>
        {
            tran.Insert(sale);

            foreach (var item in copiedItems)
            {
                item.SaleId = sale.Id;
                tran.Insert(item);

                var product = tran.Table<Product>()
                    .Where(p => p.Id == item.ProductId)
                    .FirstOrDefault();

                if (product != null)
                {
                    product.StockQuantity -= item.Quantity;
                    if (product.StockQuantity < 0)
                        product.StockQuantity = 0;
                    product.UpdatedAt = DateTime.UtcNow;
                    tran.Update(product);
                }
            }

            if (sale.IsCredit && sale.CustomerId.HasValue && sale.CreditAmount > 0)
            {
                var customer = tran.Table<Customer>()
                    .Where(c => c.Id == sale.CustomerId.Value)
                    .FirstOrDefault();

                if (customer != null)
                {
                    customer.CurrentBalance += sale.CreditAmount;
                    customer.UpdatedAt = DateTime.UtcNow;
                    tran.Update(customer);
                }
            }
        });

        return sale;
    }

    public async Task<List<Sale>> GetSalesAsync(DateTime? from = null, DateTime? to = null, string? query = null)
    {
        var queryBuilder = Database.Table<Sale>()
            .Where(s => !s.IsVoided);

        if (from.HasValue)
        {
            var fromUtc = from.Value.Date.ToUniversalTime();
            queryBuilder = queryBuilder.Where(s => s.CreatedAt >= fromUtc);
        }
        if (to.HasValue)
        {
            var toUtc = to.Value.Date.AddDays(1).AddTicks(-1).ToUniversalTime();
            queryBuilder = queryBuilder.Where(s => s.CreatedAt <= toUtc);
        }

        var sales = await queryBuilder.OrderByDescending(s => s.CreatedAt).ToListAsync();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim().ToLowerInvariant();
            sales = sales.Where(s =>
                (s.ReceiptNumber ?? string.Empty).ToLowerInvariant().Contains(q) ||
                (s.CustomerName ?? string.Empty).ToLowerInvariant().Contains(q))
                .ToList();
        }

        return sales;
    }

    public async Task<Sale?> GetSaleByIdAsync(int id)
    {
        return await Database.Table<Sale>()
            .Where(s => s.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<List<SaleItem>> GetSaleItemsAsync(int saleId)
    {
        return await Database.Table<SaleItem>()
            .Where(si => si.SaleId == saleId)
            .ToListAsync();
    }

    public async Task<DailySalesSummary> GetDailySalesSummaryAsync(DateTime date)
    {
        var startOfDayUtc = date.Date.ToUniversalTime();
        var endOfDayUtc = date.Date.AddDays(1).AddTicks(-1).ToUniversalTime();

        var sales = await Database.Table<Sale>()
            .Where(s => !s.IsVoided && s.CreatedAt >= startOfDayUtc && s.CreatedAt <= endOfDayUtc)
            .ToListAsync();

        var saleIds = sales.Select(s => s.Id).ToList();
        var allItems = new List<SaleItem>();
        foreach (var sid in saleIds)
        {
            var items = await Database.Table<SaleItem>()
                .Where(si => si.SaleId == sid)
                .ToListAsync();
            allItems.AddRange(items);
        }

        return new DailySalesSummary
        {
            Date = date.Date,
            TotalSales = sales.Sum(s => s.GrandTotal),
            CashSales = sales.Where(s => !s.IsCredit).Sum(s => s.GrandTotal),
            CreditSales = sales.Where(s => s.IsCredit).Sum(s => s.CreditAmount),
            SalesCount = sales.Count,
            ItemsSold = (int)allItems.Sum(i => i.Quantity)
        };
    }

    public async Task<bool> VoidSaleAsync(int saleId, string reason)
    {
        var sale = await Database.Table<Sale>()
            .Where(s => s.Id == saleId)
            .FirstOrDefaultAsync();

        if (sale == null || sale.IsVoided)
            return false;

        var items = await GetSaleItemsAsync(saleId);

        await Database.RunInTransactionAsync(tran =>
        {
            foreach (var item in items)
            {
                var product = tran.Table<Product>()
                    .Where(p => p.Id == item.ProductId)
                    .FirstOrDefault();

                if (product != null)
                {
                    product.StockQuantity += item.Quantity;
                    product.UpdatedAt = DateTime.UtcNow;
                    tran.Update(product);
                }
            }

            if (sale.IsCredit && sale.CustomerId.HasValue && sale.CreditAmount > 0)
            {
                var customer = tran.Table<Customer>()
                    .Where(c => c.Id == sale.CustomerId.Value)
                    .FirstOrDefault();

                if (customer != null)
                {
                    customer.CurrentBalance -= sale.CreditAmount;
                    if (customer.CurrentBalance < 0)
                        customer.CurrentBalance = 0;
                    customer.UpdatedAt = DateTime.UtcNow;
                    tran.Update(customer);
                }
            }

            sale.IsVoided = true;
            sale.Notes = string.IsNullOrWhiteSpace(sale.Notes)
                ? $"Voided: {reason}"
                : $"{sale.Notes}\nVoided: {reason}";
            tran.Update(sale);
        });

        return true;
    }

    private static string GenerateReceiptNumber()
    {
        lock (_receiptLock)
        {
            var today = DateTime.UtcNow.Date;
            if (today != _counterDate)
            {
                _counterDate = today;
                _dailyCounter = 0;
            }

            _dailyCounter++;
            return $"DP-{today:yyyyMMdd}-{_dailyCounter:D4}";
        }
    }

    private static int _dailyCounter;
    private static DateTime _counterDate = DateTime.MinValue;
}

public class DailySalesSummary
{
    public DateTime Date { get; set; }
    public decimal TotalSales { get; set; }
    public decimal CashSales { get; set; }
    public decimal CreditSales { get; set; }
    public int SalesCount { get; set; }
    public int ItemsSold { get; set; }
}
