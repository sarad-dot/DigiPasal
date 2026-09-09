using DigiPasal.Models;
using SQLite;

namespace DigiPasal.Services;

public class CustomerService
{
    private static readonly Lazy<CustomerService> _lazyInstance = new(() => new CustomerService());

    public static CustomerService Instance => _lazyInstance.Value;

    private readonly DatabaseService _dbService;

    private CustomerService()
    {
        _dbService = DatabaseService.Instance;
    }

    private SQLiteAsyncConnection Database => _dbService.Database;

    public async Task<List<Customer>> GetActiveCustomersAsync(string? query = null)
    {
        var customers = await Database.Table<Customer>()
            .Where(c => c.IsActive)
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim().ToLowerInvariant();
            customers = customers.Where(c =>
                (c.Name ?? string.Empty).ToLowerInvariant().Contains(q) ||
                (c.Phone ?? string.Empty).ToLowerInvariant().Contains(q))
                .ToList();
        }

        return customers.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<List<Customer>> GetAllCustomersAsync()
    {
        var customers = await Database.Table<Customer>().ToListAsync();
        return customers.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<Customer?> GetCustomerAsync(int id)
    {
        return await Database.Table<Customer>()
            .Where(c => c.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<int> SaveCustomerAsync(Customer customer)
    {
        if (customer == null)
            throw new ArgumentNullException(nameof(customer));

        customer.Name = customer.Name?.Trim() ?? string.Empty;
        customer.Phone = customer.Phone?.Trim() ?? string.Empty;
        customer.Address = customer.Address?.Trim() ?? string.Empty;

        if (customer.Id == 0)
        {
            customer.CreatedAt = DateTime.UtcNow;
            customer.UpdatedAt = customer.CreatedAt;
            await Database.InsertAsync(customer);
        }
        else
        {
            customer.UpdatedAt = DateTime.UtcNow;
            await Database.UpdateAsync(customer);
        }

        return customer.Id;
    }

    public async Task<bool> DeleteCustomerAsync(int id)
    {
        var customer = await GetCustomerAsync(id);
        if (customer == null)
            return false;

        customer.IsActive = false;
        customer.UpdatedAt = DateTime.UtcNow;
        await Database.UpdateAsync(customer);
        return true;
    }

    public async Task<List<Customer>> GetCustomersWithBalanceAsync()
    {
        return await Database.Table<Customer>()
            .Where(c => c.IsActive && c.CurrentBalance > 0)
            .OrderByDescending(c => c.CurrentBalance)
            .ToListAsync();
    }

    public async Task<List<Sale>> GetCustomerTransactionHistoryAsync(int customerId)
    {
        return await Database.Table<Sale>()
            .Where(s => s.CustomerId == customerId && !s.IsVoided)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }
}
