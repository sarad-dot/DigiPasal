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

        return count;
    }

    public async Task<bool> IsDailyBookClosedAsync(DateTime date)
    {
        if (date.Date < DateTime.Today)
            return true;

        var lastClosed = await _dbService.GetSettingAsync(LastClosedSettingKey);
        return lastClosed == date.Date.ToString("yyyy-MM-dd");
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

        var entries = new List<DailyBookEntry>();
        foreach (var id in customerIds)
        {
            var customerSales = sales.Where(s => (s.CustomerId ?? 0) == id).ToList();
            var customerPayments = payments.Where(p => p.CustomerId == id).ToList();
            var customerTransfers = transfers.Where(t => t.CustomerId == id).ToList();

            var customer = customers.FirstOrDefault(c => c.Id == id);
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