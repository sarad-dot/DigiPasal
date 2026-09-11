using System.Globalization;
using DigiPasal.Models;
using SQLite;

namespace DigiPasal.Services;

public class CreditService
{
    private const string LastClosedSettingKey = "daily_book_last_closed";

    private static readonly Lazy<CreditService> _lazyInstance = new(() => new CreditService());

    public static CreditService Instance => _lazyInstance.Value;

    private readonly DatabaseService _dbService;

    private CreditService()
    {
        _dbService = DatabaseService.Instance;
    }

    private SQLiteAsyncConnection Database => _dbService.Database;

    public async Task<CreditPayment> RecordPaymentAsync(
        int customerId,
        decimal amount,
        CreditBookType book,
        int? saleId = null,
        string? notes = null,
        DateTime? paymentDate = null)
    {
        if (amount <= 0)
            throw new ArgumentException("Payment amount must be greater than zero.", nameof(amount));

        var now = DateTime.UtcNow;
        var payment = new CreditPayment
        {
            CustomerId = customerId,
            SaleId = saleId,
            Amount = amount,
            CreditBookType = (int)book,
            IsTransfer = false,
            Notes = notes ?? string.Empty,
            PaymentDate = (paymentDate ?? now).Date.ToUniversalTime(),
            CreatedAt = now
        };

        await Database.RunInTransactionAsync(tran =>
        {
            var customer = tran.Table<Customer>()
                .Where(c => c.Id == customerId)
                .FirstOrDefault();

            if (customer == null)
                throw new InvalidOperationException("Customer not found.");

            if (book == CreditBookType.Partner)
            {
                customer.PartnerBalance -= amount;
                if (customer.PartnerBalance < 0)
                    customer.PartnerBalance = 0;
            }
            else
            {
                customer.DailyBalance -= amount;
                if (customer.DailyBalance < 0)
                    customer.DailyBalance = 0;
            }

            customer.CurrentBalance = customer.DailyBalance + customer.PartnerBalance;
            customer.UpdatedAt = now;
            tran.Update(customer);
            tran.Insert(payment);
        });

        return payment;
    }

    public async Task<int> CloseDailyBookAsync(DateTime date)
    {
        var dateUtc = date.Date.ToUniversalTime();
        var now = DateTime.UtcNow;
        var count = 0;

        await Database.RunInTransactionAsync(tran =>
        {
            var customers = tran.Table<Customer>()
                .Where(c => c.DailyBalance > 0)
                .ToList();

            foreach (var customer in customers)
            {
                var carried = customer.DailyBalance;
                tran.Insert(new CreditPayment
                {
                    CustomerId = customer.Id,
                    SaleId = null,
                    Amount = carried,
                    CreditBookType = (int)CreditBookType.Partner,
                    IsTransfer = true,
                    Notes = $"Carried over from {date:dd MMM yyyy} Daily book",
                    PaymentDate = dateUtc,
                    CreatedAt = now
                });

                customer.PartnerBalance += carried;
                customer.DailyBalance = 0;
                customer.CurrentBalance = customer.PartnerBalance;
                customer.UpdatedAt = now;
                tran.Update(customer);
            }

            count = customers.Count;
        });

        await _dbService.SaveSettingAsync(LastClosedSettingKey, date.Date.ToString("yyyy-MM-dd"));

        await LogDayBookAsync(date, "Closed");

        return count;
    }

    public async Task<bool> IsDailyBookClosedAsync(DateTime date)
    {
        var startUtc = date.Date.ToUniversalTime();
        var endUtc = date.Date.AddDays(1).ToUniversalTime().AddTicks(-1);

        var transferCount = await Database.Table<CreditPayment>()
            .Where(p => p.IsTransfer && p.PaymentDate >= startUtc && p.PaymentDate <= endUtc)
            .CountAsync();

        if (transferCount > 0)
            return true;

        var lastClosed = await _dbService.GetSettingAsync(LastClosedSettingKey);
        return lastClosed == date.Date.ToString("yyyy-MM-dd");
    }

    /// <summary>
    /// Reverses a day-book close: moves the carried amounts back from the Partner
    /// book to the Daily book for the given date and records an audit entry.
    /// </summary>
    public async Task<int> ReopenDailyBookAsync(DateTime date)
    {
        var startUtc = date.Date.ToUniversalTime();
        var endUtc = date.Date.AddDays(1).ToUniversalTime().AddTicks(-1);
        var now = DateTime.UtcNow;
        var count = 0;

        await Database.RunInTransactionAsync(tran =>
        {
            var transfers = tran.Table<CreditPayment>()
                .Where(p => p.IsTransfer && p.PaymentDate >= startUtc && p.PaymentDate <= endUtc)
                .ToList();

            count = transfers.Count;

            foreach (var transfer in transfers)
            {
                var customer = tran.Table<Customer>()
                    .Where(c => c.Id == transfer.CustomerId)
                    .FirstOrDefault();

                if (customer == null)
                    continue;

                customer.PartnerBalance -= transfer.Amount;
                if (customer.PartnerBalance < 0)
                    customer.PartnerBalance = 0;

                customer.DailyBalance += transfer.Amount;
                customer.CurrentBalance = customer.DailyBalance + customer.PartnerBalance;
                customer.UpdatedAt = now;
                tran.Update(customer);

                tran.Delete(transfer);
            }
        });

        var lastClosed = await _dbService.GetSettingAsync(LastClosedSettingKey);
        var clearedKey = lastClosed == date.Date.ToString("yyyy-MM-dd");
        if (clearedKey)
            await _dbService.SaveSettingAsync(LastClosedSettingKey, string.Empty);

        if (count > 0 || clearedKey)
            await LogDayBookAsync(date, "Opened");

        return count;
    }

    /// <summary>Reopens the day's book if it is currently closed (used by backdated credit checkout).</summary>
    public async Task EnsureDailyBookOpenForAsync(DateTime date)
    {
        if (await IsDailyBookClosedAsync(date))
            await ReopenDailyBookAsync(date);
    }

    public async Task<DateTime?> GetLastClosedDateAsync()
    {
        var rows = await Database.QueryAsync<MaxDateRow>(
            "SELECT MAX(PaymentDate) AS Value FROM CreditPayments WHERE IsTransfer = 1");

        var value = rows.FirstOrDefault()?.Value;
        if (value.HasValue)
            return value.Value.ToLocalTime().Date;

        var lastClosed = await _dbService.GetSettingAsync(LastClosedSettingKey);
        if (DateTime.TryParseExact(lastClosed, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal, out var parsed))
            return parsed.Date;

        return null;
    }

    /// <summary>Builds the running open/close balance for a single day.</summary>
    public async Task<DayVoucher> GetDayVoucherAsync(DateTime date)
    {
        var startUtc = date.Date.ToUniversalTime();
        var endUtc = date.Date.AddDays(1).ToUniversalTime().AddTicks(-1);

        var sales = await Database.Table<Sale>()
            .Where(s => !s.IsVoided && s.CreatedAt >= startUtc && s.CreatedAt <= endUtc)
            .ToListAsync();

        var payments = await Database.Table<CreditPayment>()
            .Where(p => p.PaymentDate >= startUtc && p.PaymentDate <= endUtc)
            .ToListAsync();

        var opening = await GetOutstandingBeforeAsync(date);

        var cashSales = sales.Where(s => !s.IsCredit).Sum(s => s.GrandTotal);
        var creditDaily = sales
            .Where(s => s.IsCredit && s.CreditBookType == (int)CreditBookType.Daily)
            .Sum(s => s.CreditAmount);
        var creditPartner = sales
            .Where(s => s.IsCredit && s.CreditBookType == (int)CreditBookType.Partner)
            .Sum(s => s.CreditAmount);
        var creditGiven = creditDaily + creditPartner;

        var collections = payments.Where(p => !p.IsTransfer).Sum(p => p.Amount);
        var carried = payments.Where(p => p.IsTransfer).Sum(p => p.Amount);

        var saleIds = sales.Select(s => s.Id).ToList();
        var items = saleIds.Count == 0
            ? new List<SaleItem>()
            : await Database.Table<SaleItem>().Where(si => saleIds.Contains(si.SaleId)).ToListAsync();

        return new DayVoucher
        {
            Date = date.Date,
            OpeningBalance = opening,
            CashSales = cashSales,
            CreditDaily = creditDaily,
            CreditPartner = creditPartner,
            Collections = collections,
            CarriedToPartner = carried,
            SalesCount = sales.Count,
            ItemsSold = (int)items.Sum(i => i.Quantity),
            TotalSales = cashSales + creditGiven,
            ClosingBalance = opening + creditGiven - collections,
            IsClosed = await IsDailyBookClosedAsync(date)
        };
    }

    /// <summary>
    /// Total money owed to the shop (Daily + Partner, transfers excluded) as of the
    /// instant just before the given date. Closing of one day equals this for the next.
    /// </summary>
    private async Task<decimal> GetOutstandingBeforeAsync(DateTime date)
    {
        var startUtc = date.Date.ToUniversalTime();

        var creditGiven = await Database.ExecuteScalarAsync<decimal>(
            "SELECT COALESCE(SUM(CreditAmount), 0) FROM Sales " +
            "WHERE IsCredit = 1 AND IsVoided = 0 AND CreatedAt < ?", startUtc);

        var collected = await Database.ExecuteScalarAsync<decimal>(
            "SELECT COALESCE(SUM(Amount), 0) FROM CreditPayments " +
            "WHERE IsTransfer = 0 AND PaymentDate < ?", startUtc);

        return creditGiven - collected;
    }

    /// <summary>Global open/close audit trail, newest first.</summary>
    public async Task<List<DayBookLogRow>> GetDayBookLogsAsync()
    {
        var logs = await Database.Table<DayBookLog>()
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();

        return logs.Select(l => new DayBookLogRow
        {
            Timestamp = l.Timestamp,
            BookDate = l.BookDate,
            Action = l.Action,
            UserName = l.UserName,
            Note = l.Note
        }).ToList();
    }

    private async Task LogDayBookAsync(DateTime date, string action, string? note = null)
    {
        var user = await AuthService.Instance.GetCurrentUserAsync();
        var userName = !string.IsNullOrWhiteSpace(user?.FullName) ? user.FullName
            : !string.IsNullOrWhiteSpace(user?.Username) ? user.Username
            : "Unknown";

        await Database.InsertAsync(new DayBookLog
        {
            BookDate = date.Date,
            Action = action,
            UserId = user?.Id ?? 0,
            UserName = userName,
            Timestamp = DateTime.UtcNow,
            Note = note ?? string.Empty
        });
    }

    public async Task<DailyBookReport> GetDailyBookReportAsync(DateTime date)
    {
        var startUtc = date.Date.ToUniversalTime();
        var endUtc = date.Date.AddDays(1).AddTicks(-1).ToUniversalTime();

        var sales = await Database.Table<Sale>()
            .Where(s => s.IsCredit && !s.IsVoided &&
                        s.CreditBookType == (int)CreditBookType.Daily &&
                        s.CreatedAt >= startUtc && s.CreatedAt <= endUtc)
            .ToListAsync();

        var payments = await Database.Table<CreditPayment>()
            .Where(p => !p.IsTransfer &&
                        p.CreditBookType == (int)CreditBookType.Daily &&
                        p.PaymentDate >= startUtc && p.PaymentDate <= endUtc)
            .ToListAsync();

        var transfers = await Database.Table<CreditPayment>()
            .Where(p => p.IsTransfer && p.PaymentDate >= startUtc && p.PaymentDate <= endUtc)
            .ToListAsync();

        var isClosed = await IsDailyBookClosedAsync(date);

        var customerIds = new HashSet<int>();
        foreach (var s in sales) customerIds.Add(s.CustomerId ?? 0);
        foreach (var p in payments) customerIds.Add(p.CustomerId);
        foreach (var t in transfers) customerIds.Add(t.CustomerId);

        var customers = await Database.Table<Customer>().ToListAsync();
        var customerById = new Dictionary<int, Customer>(customers.Count);
        foreach (var c in customers) customerById[c.Id] = c;

        var entries = new List<DailyBookEntry>();
        foreach (var id in customerIds)
        {
            var customerSales = sales.Where(s => (s.CustomerId ?? 0) == id).ToList();
            var customerPayments = payments.Where(p => p.CustomerId == id).ToList();
            var customerTransfers = transfers.Where(t => t.CustomerId == id).ToList();

            customerById.TryGetValue(id, out var customer);
            var name = customer?.Name
                       ?? customerSales.FirstOrDefault()?.CustomerName
                       ?? (id == 0 ? "Walk-in" : string.Empty);

            var entry = new DailyBookEntry
            {
                CustomerId = id == 0 ? null : (int?)id,
                CustomerName = string.IsNullOrWhiteSpace(name) ? "Walk-in" : name,
                Sales = customerSales,
                Payments = customerPayments,
                CarriedToPartner = customerTransfers.Sum(t => t.Amount)
            };

            entries.Add(entry);
        }

        var report = new DailyBookReport
        {
            Date = date.Date,
            IsClosed = isClosed,
            Entries = entries
                .OrderByDescending(e => e.Outstanding)
                .ThenBy(e => e.CustomerName, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        return report;
    }

    public async Task<List<Customer>> GetPartnerCustomersAsync()
    {
        return await Database.Table<Customer>()
            .Where(c => c.IsActive && c.PartnerBalance > 0)
            .OrderByDescending(c => c.PartnerBalance)
            .ToListAsync();
    }

    public async Task<List<CreditHistoryEntry>> GetCustomerHistoryAsync(int customerId, CreditBookType book)
    {
        var sales = await Database.Table<Sale>()
            .Where(s => s.CustomerId == customerId && s.IsCredit && !s.IsVoided &&
                        s.CreditBookType == (int)book)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        var payments = await Database.Table<CreditPayment>()
            .Where(p => p.CustomerId == customerId && p.CreditBookType == (int)book)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();

        var entries = new List<CreditHistoryEntry>();

        foreach (var sale in sales)
        {
            entries.Add(new CreditHistoryEntry
            {
                Type = "Sale",
                Description = string.IsNullOrWhiteSpace(sale.CustomerName)
                    ? sale.ReceiptNumber
                    : $"{sale.ReceiptNumber} — {sale.CustomerName}",
                Amount = sale.CreditAmount,
                Date = sale.CreatedAt
            });
        }

        foreach (var payment in payments)
        {
            var isTransfer = payment.IsTransfer;
            entries.Add(new CreditHistoryEntry
            {
                Type = isTransfer ? "Transfer" : "Payment",
                Description = isTransfer ? payment.Notes : payment.Notes,
                Amount = payment.Amount,
                Date = payment.PaymentDate,
                IsTransfer = isTransfer
            });
        }

        return entries
            .OrderByDescending(e => e.Date)
            .ThenBy(e => e.Type == "Sale" ? 0 : 1)
            .ToList();
    }

    public async Task<decimal> GetOutstandingForBookAsync(int customerId, CreditBookType book)
    {
        var customer = await Database.Table<Customer>()
            .Where(c => c.Id == customerId)
            .FirstOrDefaultAsync();

        if (customer == null)
            return 0;

        return book == CreditBookType.Partner ? customer.PartnerBalance : customer.DailyBalance;
    }

    // ── Report: Daily Credit Summary (date range) ────────────────
    public async Task<List<DailyCreditSummaryRow>> GetDailyCreditSummaryAsync(DateTime from, DateTime to)
    {
        var startUtc = from.Date.ToUniversalTime();
        var endUtc = to.Date.AddDays(1).AddTicks(-1).ToUniversalTime();

        var sales = await Database.Table<Sale>()
            .Where(s => s.IsCredit && !s.IsVoided && s.CreatedAt >= startUtc && s.CreatedAt <= endUtc)
            .ToListAsync();

        var payments = await Database.Table<CreditPayment>()
            .Where(p => !p.IsTransfer && p.PaymentDate >= startUtc && p.PaymentDate <= endUtc)
            .ToListAsync();

        var grouped = new Dictionary<string, DailyCreditSummaryRow>();

        foreach (var s in sales)
        {
            var key = s.CreatedAt.ToLocalTime().Date.ToString("yyyy-MM-dd");
            if (!grouped.TryGetValue(key, out var row))
            {
                row = new DailyCreditSummaryRow { Date = s.CreatedAt.ToLocalTime().Date };
                grouped[key] = row;
            }
            row.CreditSales += s.CreditAmount;
            row.TrackCustomer(s.CustomerId ?? 0);
        }

        foreach (var p in payments)
        {
            var key = p.PaymentDate.ToLocalTime().Date.ToString("yyyy-MM-dd");
            if (!grouped.TryGetValue(key, out var row))
            {
                row = new DailyCreditSummaryRow { Date = p.PaymentDate.ToLocalTime().Date };
                grouped[key] = row;
            }
            row.PaymentsCollected += p.Amount;
            row.TrackCustomer(p.CustomerId);
        }

        return grouped.Values
            .OrderByDescending(r => r.Date)
            .ToList();
    }

    // ── Report: Credit Aging ──────────────────────────────────────
    public async Task<List<CreditAgingRow>> GetCreditAgingAsync()
    {
        var customers = await Database.Table<Customer>()
            .Where(c => c.IsActive && c.CurrentBalance > 0)
            .OrderByDescending(c => c.CurrentBalance)
            .ToListAsync();

        if (customers.Count == 0)
            return new List<CreditAgingRow>();

        // One grouped query instead of one query per customer (N+1)
        var lastPayments = await Database.QueryAsync<CustomerLastPayment>(
            "SELECT CustomerId, MAX(PaymentDate) AS LastPaymentDate " +
            "FROM CreditPayments WHERE IsTransfer = 0 GROUP BY CustomerId");

        var lastPaymentByCustomer = new Dictionary<int, DateTime>(lastPayments.Count);
        foreach (var lp in lastPayments)
            lastPaymentByCustomer[lp.CustomerId] = lp.LastPaymentDate;

        var result = new List<CreditAgingRow>(customers.Count);
        var today = DateTime.Today;

        foreach (var c in customers)
        {
            DateTime lastPaymentDate = lastPaymentByCustomer.TryGetValue(c.Id, out var last)
                ? last.ToLocalTime().Date
                : c.CreatedAt.ToLocalTime().Date;

            var daysSinceLastPayment = (today - lastPaymentDate).Days;
            var bucket = daysSinceLastPayment switch
            {
                <= 7 => "0-7 days",
                <= 30 => "8-30 days",
                <= 60 => "31-60 days",
                <= 90 => "61-90 days",
                _ => "90+ days"
            };

            result.Add(new CreditAgingRow
            {
                CustomerId = c.Id,
                CustomerName = c.Name,
                Phone = c.Phone,
                TotalBalance = c.CurrentBalance,
                DailyBalance = c.DailyBalance,
                PartnerBalance = c.PartnerBalance,
                DaysSinceLastPayment = daysSinceLastPayment,
                LastPaymentDate = lastPaymentByCustomer.ContainsKey(c.Id) ? lastPaymentDate : (DateTime?)null,
                AgingBucket = bucket
            });
        }

        return result;
    }

    // ── Report: Payment History ───────────────────────────────────
    public async Task<List<PaymentHistoryRow>> GetPaymentHistoryAsync(
        DateTime? from = null, DateTime? to = null, int? customerId = null)
    {
        var query = Database.Table<CreditPayment>()
            .Where(p => !p.IsTransfer);

        if (from.HasValue)
        {
            var startUtc = from.Value.Date.ToUniversalTime();
            query = query.Where(p => p.PaymentDate >= startUtc);
        }

        if (to.HasValue)
        {
            var endUtc = to.Value.Date.AddDays(1).AddTicks(-1).ToUniversalTime();
            query = query.Where(p => p.PaymentDate <= endUtc);
        }

        if (customerId.HasValue)
            query = query.Where(p => p.CustomerId == customerId.Value);

        var payments = await query.OrderByDescending(p => p.PaymentDate).ToListAsync();

        var customerIds = payments.Select(p => p.CustomerId).Distinct().ToList();
        var customers = await Database.Table<Customer>()
            .Where(c => customerIds.Contains(c.Id))
            .ToListAsync();
        var customerMap = customers.ToDictionary(c => c.Id);

        var sales = await Database.Table<Sale>()
            .Where(s => customerIds.Contains(s.CustomerId ?? 0) && s.IsCredit && !s.IsVoided)
            .ToListAsync();
        var saleMap = sales.ToDictionary(s => s.Id);

        return payments.Select(p =>
        {
            customerMap.TryGetValue(p.CustomerId, out var cust);
            var bookName = p.CreditBookType == (int)CreditBookType.Partner ? "Partner" : "Daily";
            var saleInfo = p.SaleId.HasValue && saleMap.TryGetValue(p.SaleId.Value, out var sale)
                ? $" ({sale.ReceiptNumber})"
                : string.Empty;

            return new PaymentHistoryRow
            {
                PaymentId = p.Id,
                CustomerId = p.CustomerId,
                CustomerName = cust?.Name ?? "Unknown",
                Amount = p.Amount,
                PaymentDate = p.PaymentDate.ToLocalTime(),
                BookType = bookName,
                Notes = p.Notes,
                SaleInfo = saleInfo
            };
        }).ToList();
    }

    // ── Report: Credit Trend (daily totals for chart) ────────────
    public async Task<List<CreditTrendPoint>> GetCreditTrendAsync(int days = 30)
    {
        var from = DateTime.Today.AddDays(-days);
        var startUtc = from.ToUniversalTime();
        var endUtc = DateTime.Today.AddDays(1).AddTicks(-1).ToUniversalTime();

        var sales = await Database.Table<Sale>()
            .Where(s => s.IsCredit && !s.IsVoided && s.CreatedAt >= startUtc && s.CreatedAt <= endUtc)
            .ToListAsync();

        var payments = await Database.Table<CreditPayment>()
            .Where(p => !p.IsTransfer && p.PaymentDate >= startUtc && p.PaymentDate <= endUtc)
            .ToListAsync();

        var points = new Dictionary<string, CreditTrendPoint>();
        for (var d = from; d <= DateTime.Today; d = d.AddDays(1))
        {
            var key = d.ToString("yyyy-MM-dd");
            points[key] = new CreditTrendPoint { Date = d };
        }

        foreach (var s in sales)
        {
            var key = s.CreatedAt.ToLocalTime().Date.ToString("yyyy-MM-dd");
            if (points.TryGetValue(key, out var pt))
                pt.NewCredit += s.CreditAmount;
        }

        foreach (var p in payments)
        {
            var key = p.PaymentDate.ToLocalTime().Date.ToString("yyyy-MM-dd");
            if (points.TryGetValue(key, out var pt))
                pt.Payments += p.Amount;
        }

        return points.Values.OrderBy(p => p.Date).ToList();
    }

    // ── Report: All customers with any credit balance ────────────
    public async Task<List<Customer>> GetAllCustomersWithCreditAsync()
    {
        return await Database.Table<Customer>()
            .Where(c => c.IsActive && c.CurrentBalance > 0)
            .OrderByDescending(c => c.CurrentBalance)
            .ToListAsync();
    }

    // ── Aggregates (single-row SUM queries, no materialization) ──
    public Task<decimal> GetTotalPaymentsCollectedAsync()
    {
        return Database.ExecuteScalarAsync<decimal>(
            "SELECT COALESCE(SUM(Amount), 0) FROM CreditPayments WHERE IsTransfer = 0");
    }

    public Task<decimal> GetCreditIssuedTotalAsync(DateTime from)
    {
        var fromUtc = from.ToUniversalTime();
        return Database.ExecuteScalarAsync<decimal>(
            "SELECT COALESCE(SUM(CreditAmount), 0) FROM Sales " +
            "WHERE IsCredit = 1 AND IsVoided = 0 AND CreatedAt >= ?", fromUtc);
    }
}

public class CustomerLastPayment
{
    public int CustomerId { get; set; }
    public DateTime LastPaymentDate { get; set; }
}

public class DailyBookReport
{
    public DateTime Date { get; set; }
    public bool IsClosed { get; set; }
    public decimal OutstandingTotal => Entries.Sum(e => e.Outstanding);
    public List<DailyBookEntry> Entries { get; set; } = new();
}

public class DailyBookEntry
{
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public List<Sale> Sales { get; set; } = new();
    public List<CreditPayment> Payments { get; set; } = new();
    public decimal CarriedToPartner { get; set; }

    public decimal SalesTotal => Sales.Sum(s => s.CreditAmount);
    public decimal PaymentsTotal => Payments.Sum(p => p.Amount);
    public int CustomerIdValue => CustomerId ?? 0;

    /// <summary>Remaining due: carried amount for closed books, otherwise sales minus payments.</summary>
    public decimal Outstanding => CarriedToPartner > 0
        ? CarriedToPartner
        : Math.Max(0, SalesTotal - PaymentsTotal);

    public string SalesTotalDisplay => CurrencyFormatter.Format(SalesTotal);
    public string PaymentsTotalDisplay => CurrencyFormatter.Format(PaymentsTotal);
    public string OutstandingDisplay => CurrencyFormatter.Format(Outstanding);

    public string StatusLabel => CarriedToPartner > 0
        ? "Carried to Partner"
        : Outstanding > 0
            ? "Due"
            : "Settled";

    public bool HasOutstanding => Outstanding > 0;
}

public class CreditHistoryEntry
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public bool IsTransfer { get; set; }

    public bool IsSale => Type == "Sale";
    public bool IsPayment => Type == "Payment";
    public string TypeDisplay => IsTransfer ? "Transfer" : IsSale ? "Credit Sale" : "Payment";
    public string DateDisplay => Date.ToString("dd MMM yyyy, HH:mm");
    public string AmountDisplay => CurrencyFormatter.Format(Amount);
}

public class DailyCreditSummaryRow
    {
        private readonly HashSet<int> _customerIds = new();

        public DateTime Date { get; set; }
        public decimal CreditSales { get; set; }
        public decimal PaymentsCollected { get; set; }
        public decimal NetOutstanding => CreditSales - PaymentsCollected;

        public void TrackCustomer(int customerId)
        {
            if (customerId > 0)
                _customerIds.Add(customerId);
        }

        public string DateDisplay => NepaliDateConverter.Format(Date);
        public string DateShortDisplay => NepaliDateConverter.FormatShort(Date);
        public string CreditSalesDisplay => CurrencyFormatter.Format(CreditSales);
        public string PaymentsDisplay => CurrencyFormatter.Format(PaymentsCollected);
        public string NetDisplay => CurrencyFormatter.Format(NetOutstanding);
        public bool HasOutstanding => NetOutstanding > 0;
        public string CustomerCountDisplay =>
            _customerIds.Count == 1 ? "1 customer" : $"{_customerIds.Count} customers";
    }

public class CreditAgingRow
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public decimal TotalBalance { get; set; }
    public decimal DailyBalance { get; set; }
    public decimal PartnerBalance { get; set; }
    public int DaysSinceLastPayment { get; set; }
    public DateTime? LastPaymentDate { get; set; }
    public string AgingBucket { get; set; } = string.Empty;

    public string BalanceDisplay => CurrencyFormatter.Format(TotalBalance);
    public string LastPaymentDisplay => LastPaymentDate?.ToString("dd MMM yyyy") ?? "Never";
    public string DaysDisplay => DaysSinceLastPayment == 0 ? "Today" : $"{DaysSinceLastPayment}d ago";
    public bool IsCritical => DaysSinceLastPayment > 90;
    public bool IsWarning => DaysSinceLastPayment > 30 && DaysSinceLastPayment <= 90;
}

public class PaymentHistoryRow
{
    public int PaymentId { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string BookType { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string SaleInfo { get; set; } = string.Empty;

    public string AmountDisplay => CurrencyFormatter.Format(Amount);
    public string DateDisplay => NepaliDateConverter.Format(PaymentDate);
    public string DateShortDisplay => NepaliDateConverter.FormatShort(PaymentDate);
    public string BookTypeShort => BookType == "Partner" ? "P" : "D";
    public string DetailDisplay => $"{BookType} book{SaleInfo}";
}

public class CreditTrendPoint
{
    public DateTime Date { get; set; }
    public decimal NewCredit { get; set; }
    public decimal Payments { get; set; }

    public string DateLabel => Date.ToString("dd MMM");
    public decimal Outstanding => NewCredit - Payments;
}

public class MaxDateRow
{
    public DateTime? Value { get; set; }
}

/// <summary>
/// A single day's settlement voucher. ClosingBalance is the total money owed to the
/// shop at the end of the day and is, by construction, the OpeningBalance of the
/// next day, so backdated entries cascade forward automatically.
/// </summary>
public class DayVoucher
{
    public DateTime Date { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal CashSales { get; set; }
    public decimal CreditDaily { get; set; }
    public decimal CreditPartner { get; set; }
    public decimal Collections { get; set; }
    public decimal CarriedToPartner { get; set; }
    public int SalesCount { get; set; }
    public int ItemsSold { get; set; }
    public decimal TotalSales { get; set; }
    public decimal ClosingBalance { get; set; }
    public bool IsClosed { get; set; }

    public decimal CreditGiven => CreditDaily + CreditPartner;

    public string DateNepali => NepaliDateConverter.FormatWeekday(Date);
    public string DateNepaliShort => NepaliDateConverter.Format(Date);
    public string NextDayLabel => NepaliDateConverter.FormatShort(Date.AddDays(1));
    public string OpeningDisplay => CurrencyFormatter.Format(OpeningBalance);
    public string CashSalesDisplay => CurrencyFormatter.Format(CashSales);
    public string CreditDailyDisplay => CurrencyFormatter.Format(CreditDaily);
    public string CreditPartnerDisplay => CurrencyFormatter.Format(CreditPartner);
    public string CreditGivenDisplay => CurrencyFormatter.Format(CreditGiven);
    public string CollectionsDisplay => CurrencyFormatter.Format(Collections);
    public string CarriedDisplay => CurrencyFormatter.Format(CarriedToPartner);
    public string TotalSalesDisplay => CurrencyFormatter.Format(TotalSales);
    public string ClosingDisplay => CurrencyFormatter.Format(ClosingBalance);
    public string SalesCountDisplay => $"{SalesCount} {(SalesCount == 1 ? "sale" : "sales")}";
    public string ItemsSoldDisplay => $"{ItemsSold} {(ItemsSold == 1 ? "item" : "items")}";
    public string StatusLabel => IsClosed ? "CLOSED" : "OPEN";
    public bool HasCarried => CarriedToPartner > 0;
}

public class DayBookLogRow
{
    public DateTime Timestamp { get; set; }
    public DateTime BookDate { get; set; }
    public string Action { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;

    public bool IsOpened => Action.Equals("Opened", StringComparison.OrdinalIgnoreCase);
    public string TimestampDisplay => NepaliDateConverter.FormatWeekday(Timestamp.ToLocalTime());
    public string TimeDisplay => Timestamp.ToLocalTime().ToString("h:mm tt");
    public string BookDateDisplay => NepaliDateConverter.FormatShort(BookDate);
    public string BookDateFullDisplay => NepaliDateConverter.Format(BookDate);
    public string ActionLabel => IsOpened ? "Opened" : "Closed";
}