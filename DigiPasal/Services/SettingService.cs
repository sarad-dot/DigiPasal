using System.Globalization;
using DigiPasal.Models;

namespace DigiPasal.Services;

public class SettingService
{
    private static readonly Lazy<SettingService> _lazyInstance = new(() => new SettingService());

    public static SettingService Instance => _lazyInstance.Value;

    private readonly DatabaseService _dbService;

    private SettingService()
    {
        _dbService = DatabaseService.Instance;
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        return await _dbService.GetSettingAsync(key);
    }

    public async Task SaveSettingAsync(string key, string value)
    {
        await _dbService.SaveSettingAsync(key, value);
    }

    public async Task<ShopSettings> GetShopSettingsAsync()
    {
        return new ShopSettings
        {
            ShopName = await GetSettingAsync("shop_name") ?? "DigiPasal",
            ShopAddress = await GetSettingAsync("shop_address") ?? "",
            ShopPhone = await GetSettingAsync("shop_phone") ?? "",
            CurrencySymbol = await GetSettingAsync("currency_symbol") ?? "रु",
            TaxRate = decimal.TryParse(await GetSettingAsync("tax_rate"), NumberStyles.Any, CultureInfo.InvariantCulture, out var tax) ? tax : 0,
            LowStockDefault = double.TryParse(await GetSettingAsync("low_stock_default"), NumberStyles.Any, CultureInfo.InvariantCulture, out var stock) ? stock : 10
        };
    }

    public async Task SaveShopSettingsAsync(ShopSettings settings)
    {
        await SaveSettingAsync("shop_name", settings.ShopName);
        await SaveSettingAsync("shop_address", settings.ShopAddress);
        await SaveSettingAsync("shop_phone", settings.ShopPhone);
        await SaveSettingAsync("currency_symbol", settings.CurrencySymbol);
        await SaveSettingAsync("tax_rate", settings.TaxRate.ToString(CultureInfo.InvariantCulture));
        await SaveSettingAsync("low_stock_default", settings.LowStockDefault.ToString(CultureInfo.InvariantCulture));
    }
}

public class ShopSettings
{
    public string ShopName { get; set; } = "DigiPasal";
    public string ShopAddress { get; set; } = "";
    public string ShopPhone { get; set; } = "";
    public string CurrencySymbol { get; set; } = "रु";
    public decimal TaxRate { get; set; }
    public double LowStockDefault { get; set; } = 10;
}
