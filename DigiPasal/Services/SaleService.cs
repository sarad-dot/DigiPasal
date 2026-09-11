using DigiPasal.Models;
using SQLite;

namespace DigiPasal.Services;

public class SaleService
{
    private static readonly Lazy<SaleService> _lazyInstance = new(() => new SaleService());

    public static SaleService Instance => _lazyInstance.Value;

    private readonly DatabaseService _dbService;
    private static readonly SemaphoreSlim _receiptLock = new(1, 1);
    private static int _dailyCounter;
    private static DateTime _counterDate = DateTime.MinValue;

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

        sale.CreatedAt = sale.CreatedAt == default ? DateTime.UtcNow : sale.CreatedAt.ToUniversalTime();
        sale.ReceiptNumber = await GenerateReceiptNumberAsync(sale.CreatedAt);

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
                    if (sale.CreditBookType == (int)CreditBookType.Partner)
                    {
                        customer.PartnerBalance += sale.CreditAmount;
                    }
                    else
                    {
                        customer.DailyBalance += sale.CreditAmount;
                    }
                    customer.CurrentBalance = customer.DailyBalance + customer.PartnerBalance;
                    customer.UpdatedAt = DateTime.UtcNow;
                    tran.Update(customer);
                }
            }
        });

        return sale;
    }

    public async Task<List<Sale>> GetSalesAsync(DateTime? from = null, DateTime? to = null, string? query = null, int limit = 500)
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

        var q = query?.Trim();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var lower = q.ToLower();
            queryBuilder = queryBuilder.Where(s =>
                s.ReceiptNumber.ToLower().Contains(lower) ||
                s.CustomerName.ToLower().Contains(lower));
        }

        return await queryBuilder
            .OrderByDescending(s => s.CreatedAt)
            .Take(limit)
            .ToListAsync();
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

    public async Task<List<SaleListItem>> GetSalesListItemAsync(
        DateTime? from = null,
        DateTime? to = null,
        string? query = null,
        int limit = 500)
    {
        var sql = new System.Text.StringBuilder(
            "SELECT S.Id, S.ReceiptNumber, S.Subtotal, S.DiscountAmount, S.TaxAmount, " +
            "S.TotalAmount, S.GrandTotal, S.AmountPaid, S.CreditAmount, S.PaymentMethod, " +
            "S.IsCredit, S.CreditBookType, S.CustomerId, S.CustomerName, S.UserId, S.Notes, " +
            "S.IsVoided, S.IsQuickSale, S.CreatedAt, " +
            "COUNT(SI.Id) AS ItemCount, COALESCE(SUM(SI.Quantity), 0) AS UnitCount " +
            "FROM Sales S " +
            "LEFT JOIN SaleItems SI ON SI.SaleId = S.Id " +
            "WHERE S.IsVoided = 0 ");

        var conditions = new List<string>();
        var parameters = new List<object>();

        if (from.HasValue)
        {
            conditions.Add("S.CreatedAt >= ?");
            parameters.Add(from.Value.Date.ToUniversalTime());
        }
        if (to.HasValue)
        {
            conditions.Add("S.CreatedAt <= ?");
            parameters.Add(to.Value.Date.AddDays(1).AddTicks(-1).ToUniversalTime());
        }
        if (!string.IsNullOrWhiteSpace(query))
        {
            var like = $"%{query.Trim().ToLower()}%";
            conditions.Add("(LOWER(S.ReceiptNumber) LIKE ? OR LOWER(S.CustomerName) LIKE ?)");
            parameters.Add(like);
            parameters.Add(like);
        }

        if (conditions.Count > 0)
            sql.Append("AND " + string.Join(" AND ", conditions) + " ");

        sql.Append("GROUP BY S.Id ORDER BY S.CreatedAt DESC, S.Id DESC LIMIT ?");
        parameters.Add(limit);

        return await Database.QueryAsync<SaleListItem>(sql.ToString(), parameters.ToArray());
    }

    public async Task<DailySalesSummary> GetDailySalesSummaryAsync(DateTime date)
    {
        var startOfDayUtc = date.Date.ToUniversalTime();
        var endOfDayUtc = date.Date.AddDays(1).AddTicks(-1).ToUniversalTime();

        var sales = await Database.Table<Sale>()
            .Where(s => !s.IsVoided && s.CreatedAt >= startOfDayUtc && s.CreatedAt <= endOfDayUtc)
            .ToListAsync();

        var saleIds = sales.Select(s => s.Id).ToList();
        var allItems = await Database.Table<SaleItem>()
            .Where(si => saleIds.Contains(si.SaleId))
            .ToListAsync();

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

    public async Task<List<DailySalesSummary>> GetDailySalesReportAsync(DateTime? from = null, DateTime? to = null)
    {
        var rows = await Database.QueryAsync<SalesDayRow>(
            "SELECT date(CreatedAt) AS DayKey, " +
            "SUM(GrandTotal) AS TotalSales, " +
            "SUM(CASE WHEN IsCredit = 0 THEN GrandTotal ELSE 0 END) AS CashSales, " +
            "SUM(CASE WHEN IsCredit = 1 THEN CreditAmount ELSE 0 END) AS CreditSales, " +
            "COUNT(*) AS SalesCount, " +
            "0 AS ItemsSold " +
            "FROM Sales WHERE IsVoided = 0 " +
            $"AND date(CreatedAt) >= date('{(from ?? DateTime.Today):yyyy-MM-dd}') " +
            $"AND date(CreatedAt) <= date('{(to ?? DateTime.Today):yyyy-MM-dd}') " +
            "GROUP BY date(CreatedAt) ORDER BY date(CreatedAt) DESC");

        var dayKeys = rows.Select(r => r.DayKey).ToList();
        if (dayKeys.Count == 0)
            return new List<DailySalesSummary>();

        var clauses = string.Join(",", dayKeys.Select(k => $"'{k}'"));
        var items = await Database.QueryAsync<ItemsDayRow>(
            "SELECT date(S.CreatedAt) AS DayKey, SUM(SI.Quantity) AS ItemsSold " +
            "FROM Sales S INNER JOIN SaleItems SI ON SI.SaleId = S.Id " +
            "WHERE S.IsVoided = 0 AND date(S.CreatedAt) IN (" + clauses + ") " +
            "GROUP BY date(S.CreatedAt)");

        var itemsByDay = items.ToDictionary(i => i.DayKey, i => (int)i.ItemsSold);

        var result = new List<DailySalesSummary>(rows.Count);
        foreach (var row in rows)
        {
            result.Add(new DailySalesSummary
            {
                Date = DateTime.Parse(row.DayKey).Date,
                TotalSales = row.TotalSales,
                CashSales = row.CashSales,
                CreditSales = row.CreditSales,
                SalesCount = row.SalesCount,
                ItemsSold = itemsByDay.TryGetValue(row.DayKey, out var sold) ? sold : 0
            });
        }

        return result;
    }

    public async Task<List<ProfitDayRow>> GetProfitReportAsync(DateTime? from = null, DateTime? to = null)
    {
        return await Database.QueryAsync<ProfitDayRow>(
            "SELECT date(S.CreatedAt) AS DayKey, " +
            "SUM(S.GrandTotal) AS Revenue, " +
            "COALESCE(SUM(SI.Quantity * COALESCE(P.CostPrice, 0)), 0) AS CostOfGoods, " +
            "COUNT(DISTINCT S.Id) AS SalesCount " +
            "FROM Sales S " +
            "LEFT JOIN SaleItems SI ON SI.SaleId = S.Id " +
            "LEFT JOIN Products P ON P.Id = SI.ProductId " +
            "WHERE S.IsVoided = 0 " +
            $"AND date(S.CreatedAt) >= date('{(from ?? DateTime.Today):yyyy-MM-dd}') " +
            $"AND date(S.CreatedAt) <= date('{(to ?? DateTime.Today):yyyy-MM-dd}') " +
            "GROUP BY date(S.CreatedAt) ORDER BY date(S.CreatedAt) DESC");
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
                    if (sale.CreditBookType == (int)CreditBookType.Partner)
                    {
                        customer.PartnerBalance -= sale.CreditAmount;
                        if (customer.PartnerBalance < 0)
                            customer.PartnerBalance = 0;
                    }
                    else
                    {
                        customer.DailyBalance -= sale.CreditAmount;
                        if (customer.DailyBalance < 0)
                            customer.DailyBalance = 0;
                    }
                    customer.CurrentBalance = customer.DailyBalance + customer.PartnerBalance;
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

    public async Task<Sale> CreateQuickSaleAsync(Sale sale)
    {
        if (sale == null)
            throw new ArgumentNullException(nameof(sale));

        sale.CreatedAt = sale.CreatedAt == default ? DateTime.UtcNow : sale.CreatedAt.ToUniversalTime();
        sale.ReceiptNumber = await GenerateReceiptNumberAsync(sale.CreatedAt);
        sale.IsQuickSale = true;
        sale.Subtotal = sale.GrandTotal;
        sale.TotalAmount = sale.GrandTotal;

        await Database.RunInTransactionAsync(tran =>
        {
            tran.Insert(sale);

            if (sale.IsCredit && sale.CustomerId.HasValue && sale.CreditAmount > 0)
            {
                var customer = tran.Table<Customer>()
                    .Where(c => c.Id == sale.CustomerId.Value)
                    .FirstOrDefault();

                if (customer != null)
                {
                    if (sale.CreditBookType == (int)CreditBookType.Partner)
                    {
                        customer.PartnerBalance += sale.CreditAmount;
                    }
                    else
                    {
                        customer.DailyBalance += sale.CreditAmount;
                    }
                    customer.CurrentBalance = customer.DailyBalance + customer.PartnerBalance;
                    customer.UpdatedAt = DateTime.UtcNow;
                    tran.Update(customer);
                }
            }
        });

        return sale;
    }

    /// <summary>
    /// Builds DP-yyyyMMdd-#### using the sale's own date (so backdated receipts carry
    /// the right prefix), seeding from the highest existing receipt number for that date
    /// so app restarts do not reuse numbers and hit UNIQUE on Sales.ReceiptNumber.
    /// </summary>
    private async Task<string> GenerateReceiptNumberAsync(DateTime saleDate)
    {
        var day = saleDate.Date;
        var prefix = $"DP-{day:yyyyMMdd}-";

        await _receiptLock.WaitAsync();
        try
        {
            if (day == _counterDate.Date)
            {
                _dailyCounter++;
                return $"{prefix}{_dailyCounter:D4}";
            }

            _counterDate = day;
            _dailyCounter = await GetMaxDailySequenceAsync(prefix);
            _dailyCounter++;
            return $"{prefix}{_dailyCounter:D4}";
        }
        finally
        {
            _receiptLock.Release();
        }
    }

    private async Task<int> GetMaxDailySequenceAsync(string prefix)
    {
        var lastNumber = await Database.ExecuteScalarAsync<string>(
            "SELECT ReceiptNumber FROM Sales WHERE ReceiptNumber LIKE ? ORDER BY ReceiptNumber DESC LIMIT 1",
            prefix + "%");

        if (string.IsNullOrWhiteSpace(lastNumber))
            return 0;

        var parts = lastNumber.Split('-');
        if (parts.Length >= 3 && int.TryParse(parts[^1], out var sequence))
            return sequence;

        return 0;
    }
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

public class SalesDayRow
{
    public string DayKey { get; set; } = string.Empty;
    public decimal TotalSales { get; set; }
    public decimal CashSales { get; set; }
    public decimal CreditSales { get; set; }
    public int SalesCount { get; set; }
    public double ItemsSold { get; set; }
}

public class ItemsDayRow
{
    public string DayKey { get; set; } = string.Empty;
    public double ItemsSold { get; set; }
}

public class ProfitDayRow
{
    public string DayKey { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal CostOfGoods { get; set; }
    public int SalesCount { get; set; }
    public decimal Profit => Revenue - CostOfGoods;
}

public class SaleListItem
{
    public int Id { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal CreditAmount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public bool IsCredit { get; set; }
    public int CreditBookType { get; set; }
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool IsVoided { get; set; }
    public bool IsQuickSale { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ItemCount { get; set; }
    public int UnitCount { get; set; }
}
