using System.Windows.Input;
using DigiPasal.Services;

namespace DigiPasal.ViewModels;

public class DayVoucherViewModel : BaseViewModel, IQueryAttributable
{
    private readonly CreditService _creditService;

    private DateTime _date = DateTime.Today;
    private DayVoucher? _voucher;
    private string _shopName = string.Empty;

    public DayVoucherViewModel()
    {
        _creditService = CreditService.Instance;
        Title = "Day Voucher";
        AddSaleCommand = new Command(async () => await AddSaleAsync());
    }

    public ICommand AddSaleCommand { get; }

    public DayVoucher? Voucher
    {
        get => _voucher;
        set
        {
            if (SetProperty(ref _voucher, value))
            {
                OnPropertyChanged(nameof(HasVoucher));
                OnPropertyChanged(nameof(IsClosed));
            }
        }
    }

    public string ShopName
    {
        get => _shopName;
        set => SetProperty(ref _shopName, value);
    }

    public bool HasVoucher => Voucher != null;
    public bool IsClosed => Voucher?.IsClosed == true;
    public string StatusLabel => IsClosed ? "CLOSED" : "OPEN";

    public string DateNepali => Voucher?.DateNepali
        ?? NepaliDateConverter.FormatWeekday(_date);

    public string ClosingNote => Voucher?.NextDayLabel is { } next
        ? $"= opening balance on {next}"
        : string.Empty;

    public void ApplyQueryAttributes(IDictionary<string, object?> query)
    {
        if (query.TryGetValue("date", out var value) && value is string dateStr
            && NepaliDateConverter.TryParseAny(dateStr, out var parsed))
        {
            _date = parsed.Date;
        }
    }

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var shopSetting = await DatabaseService.Instance.GetSettingAsync("shop_name");
            ShopName = string.IsNullOrWhiteSpace(shopSetting) ? "DigiPasal" : shopSetting;
            Voucher = await _creditService.GetDayVoucherAsync(_date);
            OnPropertyChanged(nameof(DateNepali));
            OnPropertyChanged(nameof(ClosingNote));
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to load voucher: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task AddSaleAsync()
    {
        CartState.Instance.SaleDate = _date;
        await NavigationGuard.GoToAsync("Sales");
    }
}