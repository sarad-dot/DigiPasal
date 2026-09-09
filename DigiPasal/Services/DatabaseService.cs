using DigiPasal.Models;
using SQLite;

namespace DigiPasal.Services;

public class DatabaseService
{
    private static readonly Lazy<DatabaseService> _lazyInstance = new(() => new DatabaseService());

    public static DatabaseService Instance => _lazyInstance.Value;

    private SQLiteAsyncConnection? _database;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public string DatabasePath { get; }
    public string BackupsDirectory { get; }

    public SQLiteAsyncConnection Database => _database
        ?? throw new InvalidOperationException("Database not initialized. Call InitializeAsync() first.");

    private DatabaseService()
    {
        DatabasePath = Path.Combine(FileSystem.AppDataDirectory, "digipasal.db");
        BackupsDirectory = Path.Combine(FileSystem.AppDataDirectory, "backups");
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized)
                return;

            Directory.CreateDirectory(BackupsDirectory);

            _database = new SQLiteAsyncConnection(DatabasePath);

            await RunMigrationsAsync();

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task ExecuteAsync(string sql)
    {
        EnsureConnected();
        await _database!.ExecuteAsync(sql);
    }

    public async Task CloseAsync()
    {
        if (_database != null)
        {
            await _database.CloseAsync();
            _database = null;
            _initialized = false;
        }
    }

    public async Task ReconnectAsync()
    {
        await CloseAsync();
        _database = new SQLiteAsyncConnection(DatabasePath);
        await RunMigrationsAsync();
        _initialized = true;
    }

    private void EnsureConnected()
    {
        if (_database == null)
            throw new InvalidOperationException("Database connection is closed.");
    }

    private async Task RunMigrationsAsync()
    {
        var migrations = new Dictionary<int, Func<Task>>
        {
            { 1, Migration_V1_InitialSchema },
            { 2, Migration_V2_ProductsCustomersSettings },
            { 3, Migration_V3_SalesSaleItems },
            { 4, Migration_V4_QuickSale }
        };

        int currentVersion = await GetCurrentVersionAsync();

        var pendingVersions = migrations.Keys
            .Where(v => v > currentVersion)
            .OrderBy(v => v)
            .ToList();

        foreach (var version in pendingVersions)
        {
            await migrations[version]();
            await SetVersionAsync(version);
        }
    }

    private async Task<int> GetCurrentVersionAsync()
    {
        var result = await _database!.ExecuteScalarAsync<long>("PRAGMA user_version");
        return (int)result;
    }

    private async Task SetVersionAsync(int version)
    {
        await _database!.ExecuteAsync($"PRAGMA user_version = {version}");
    }

    private async Task Migration_V1_InitialSchema()
    {
        await _database!.CreateTableAsync<User>();
    }

    private async Task Migration_V2_ProductsCustomersSettings()
    {
        await _database!.CreateTableAsync<Product>();
        await _database!.CreateTableAsync<Customer>();
        await _database!.CreateTableAsync<Setting>();

        var defaults = new Dictionary<string, (string Value, string Description)>
        {
            { "shop_name", ("DigiPasal", "Shop name displayed on receipts") },
            { "shop_address", ("", "Shop address displayed on receipts") },
            { "shop_phone", ("", "Shop phone displayed on receipts") },
            { "currency_symbol", ("रु", "Currency symbol for receipts") },
            { "tax_rate", ("0", "Default tax rate percentage") },
            { "low_stock_default", ("10", "Default low stock threshold for new products") }
        };

        foreach (var (key, (value, description)) in defaults)
        {
            await _database!.ExecuteAsync(
                "INSERT OR IGNORE INTO Settings (Key, Value, Description) VALUES (?, ?, ?)",
                key, value, description);
        }
    }

    private async Task Migration_V3_SalesSaleItems()
    {
        await _database!.CreateTableAsync<Sale>();
        await _database!.CreateTableAsync<SaleItem>();
    }

    private async Task Migration_V4_QuickSale()
    {
        var columns = await _database!.QueryAsync<ColumnInfo>("PRAGMA table_info(Sales)");
        if (!columns.Any(c => string.Equals(c.Name, "IsQuickSale", StringComparison.OrdinalIgnoreCase)))
        {
            await _database!.ExecuteAsync("ALTER TABLE Sales ADD COLUMN IsQuickSale INTEGER NOT NULL DEFAULT 0");
        }
    }

    private class ColumnInfo
    {
        public string Name { get; set; } = string.Empty;
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        EnsureConnected();
        var setting = await _database!.Table<Setting>()
            .Where(s => s.Key == key)
            .FirstOrDefaultAsync();
        return setting?.Value;
    }

    public async Task SaveSettingAsync(string key, string value)
    {
        EnsureConnected();
        await _database!.ExecuteAsync(
            "INSERT OR REPLACE INTO Settings (Key, Value, Description) VALUES (?, ?, COALESCE((SELECT Description FROM Settings WHERE Key = ?), ''))",
            key, value, key);
    }
}
