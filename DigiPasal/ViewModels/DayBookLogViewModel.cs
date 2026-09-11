using System.Collections.ObjectModel;
using DigiPasal.Services;

namespace DigiPasal.ViewModels;

public class DayBookLogViewModel : BaseViewModel
{
    private readonly CreditService _creditService;
    private string _footer = string.Empty;

    public DayBookLogViewModel()
    {
        _creditService = CreditService.Instance;
        Title = "Book Open / Close Log";
    }

    public ObservableCollection<DayBookLogRow> Logs { get; } = new();

    public string Footer
    {
        get => _footer;
        set => SetProperty(ref _footer, value);
    }

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            Logs.Clear();
            var rows = await _creditService.GetDayBookLogsAsync();
            foreach (var row in rows)
                Logs.Add(row);

            var total = Logs.Count;
            Footer = Logs.Count == 0
                ? "No day book events yet. Close the daily book from the Credit page to begin the log."
                : $"{total} {(total == 1 ? "event" : "events")}";
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Failed to load log: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }
}