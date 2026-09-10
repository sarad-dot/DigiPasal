using System.Windows.Input;
using DigiPasal.Services;

namespace DigiPasal.ViewModels;

public class ShopSettingsViewModel : BaseViewModel
{
    private string _shopName = string.Empty;
    private string _shopAddress = string.Empty;
    private string _shopPhone = string.Empty;
    private string _currencySymbol = "रु";
    private string _taxRate = "0";
    private string _lowStockDefault = "10";
    private bool _isSaving;

    private readonly SettingService _settingService;

    public ShopSettingsViewModel()
    {
        _settingService = SettingService.Instance;
        Title = "Shop Settings";

        SaveCommand = new Command(async () => await SaveAsync());
    }

    public string ShopName
    {
        get => _shopName;
        set => SetProperty(ref _shopName, value);
    }

    public string ShopAddress
    {
        get => _shopAddress;
        set => SetProperty(ref _shopAddress, value);
    }

    public string ShopPhone
    {
        get => _shopPhone;
        set => SetProperty(ref _shopPhone, value);
    }

    public string CurrencySymbol
    {
        get => _currencySymbol;
        set => SetProperty(ref _currencySymbol, value);
    }

    public string TaxRate
    {
        get => _taxRate;
        set => SetProperty(ref _taxRate, value);
    }

    public string LowStockDefault
    {
        get => _lowStockDefault;
        set => SetProperty(ref _lowStockDefault, value);
    }

    public bool IsSaving
    {
        get => _isSaving;
        set
        {
            if (SetProperty(ref _isSaving, value))
                OnPropertyChanged(nameof(IsNotBusyAndNotSaving));
        }
    }

    public bool IsNotBusyAndNotSaving => IsNotBusy && !IsSaving;

    public ICommand SaveCommand { get; }

    public async Task LoadAsync()
    {
        try
        {
            var settings = await _settingService.GetShopSettingsAsync();
            ShopName = settings.ShopName;
            ShopAddress = settings.ShopAddress;
            ShopPhone = settings.ShopPhone;
            CurrencySymbol = settings.CurrencySymbol;
            TaxRate = settings.TaxRate.ToString(System.Globalization.CultureInfo.InvariantCulture);
            LowStockDefault = settings.LowStockDefault.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to load shop settings: {ex.Message}", "OK");
        }
    }

    private async Task SaveAsync()
    {
        if (IsSaving)
            return;

        IsSaving = true;
        try
        {
            if (string.IsNullOrWhiteSpace(ShopName))
            {
                await Shell.Current.DisplayAlertAsync("Shop Name Required", "Please enter the shop name.", "OK");
                return;
            }

            var currency = string.IsNullOrWhiteSpace(CurrencySymbol)
                ? "रु"
                : CurrencySymbol.Trim();

            var okTax = decimal.TryParse(TaxRate,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var taxRate);
            var okStock = double.TryParse(LowStockDefault,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var lowStockDefault);

            var settings = new ShopSettings
            {
                ShopName = ShopName.Trim(),
                ShopAddress = ShopAddress.Trim(),
                ShopPhone = ShopPhone.Trim(),
                CurrencySymbol = currency,
                TaxRate = okTax ? taxRate : 0,
                LowStockDefault = okStock ? lowStockDefault : 10
            };

            await _settingService.SaveShopSettingsAsync(settings);
            await Shell.Current.DisplayAlertAsync("Saved", "Shop settings updated.", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to save settings: {ex.Message}", "OK");
        }
        finally
        {
            IsSaving = false;
        }
    }
}