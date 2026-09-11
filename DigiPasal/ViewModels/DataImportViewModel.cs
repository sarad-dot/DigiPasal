using System.Collections.ObjectModel;
using System.Windows.Input;
using DigiPasal.Services;
using Microsoft.Maui.Storage;

namespace DigiPasal.ViewModels;

public class DataImportViewModel : BaseViewModel, IQueryAttributable
{
    private static readonly FilePickerFileType SpreadsheetTypes = new(
        new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.Android] = new[]
            {
                "text/csv",
                "text/comma-separated-values",
                "application/vnd.ms-excel",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            },
            [DevicePlatform.WinUI] = new[] { ".xlsx", ".csv" },
            [DevicePlatform.iOS] = new[] { "public.comma-separated-values-text", "org.openxmlformats.spreadsheetml.sheet" },
            [DevicePlatform.MacCatalyst] = new[] { "public.comma-separated-values-text", "org.openxmlformats.spreadsheetml.sheet" }
        });

    private readonly ImportService _importService;

    private ImportType _type = ImportType.Partners;
    private string _selectedFile = string.Empty;
    private string _fileName = string.Empty;
    private string _fileError = string.Empty;
    private string _previewNote = string.Empty;
    private bool _isImporting;

    public DataImportViewModel()
    {
        _importService = ImportService.Instance;
        Title = "Import Data";

        PickFileCommand = new Command(async () => await PickFileAsync());
        DownloadTemplateCommand = new Command(async () => await DownloadTemplateAsync());
        ImportCommand = new Command(async () => await ImportAsync());
        SelectPartnersCommand = new Command(() => SetType(ImportType.Partners));
        SelectInventoryCommand = new Command(() => SetType(ImportType.Inventory));
        SelectSalesCommand = new Command(() => SetType(ImportType.Sales));
    }

    public ICommand PickFileCommand { get; }
    public ICommand DownloadTemplateCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand SelectPartnersCommand { get; }
    public ICommand SelectInventoryCommand { get; }
    public ICommand SelectSalesCommand { get; }

    public ObservableCollection<ImportRow> Rows { get; } = new();

    public string TitleText => ImportService.TitleFor(_type);

    public string Hint => ImportService.HintFor(_type);

    public string FileName
    {
        get => _fileName;
        set
        {
            if (SetProperty(ref _fileName, value))
                OnPropertyChanged(nameof(HasFile));
        }
    }

    public bool HasFile => !string.IsNullOrWhiteSpace(FileName);

    public string FileError
    {
        get => _fileError;
        set
        {
            if (SetProperty(ref _fileError, value))
                OnPropertyChanged(nameof(HasFileError));
        }
    }

    public bool HasFileError => !string.IsNullOrWhiteSpace(FileError);

    public string PreviewNote
    {
        get => _previewNote;
        set => SetProperty(ref _previewNote, value);
    }

    public bool HasPreview => Rows.Count > 0;

    public bool IsImporting
    {
        get => _isImporting;
        set => SetProperty(ref _isImporting, value);
    }

    public bool IsPartnersSelected => _type == ImportType.Partners;
    public bool IsInventorySelected => _type == ImportType.Inventory;
    public bool IsSalesSelected => _type == ImportType.Sales;

    private void SetType(ImportType type)
    {
        if (_type == type)
            return;

        _type = type;
        FileName = string.Empty;
        FileError = string.Empty;
        PreviewNote = string.Empty;
        Rows.Clear();

        OnPropertyChanged(nameof(TitleText));
        OnPropertyChanged(nameof(Hint));
        OnPropertyChanged(nameof(IsPartnersSelected));
        OnPropertyChanged(nameof(IsInventorySelected));
        OnPropertyChanged(nameof(IsSalesSelected));
        OnPropertyChanged(nameof(HasPreview));
        OnPropertyChanged(nameof(HasFile));
    }

    public void ApplyQueryAttributes(IDictionary<string, object?> query)
    {
        if (query.TryGetValue("type", out var value))
        {
            var typeStr = value?.ToString()?.ToLowerInvariant();
            var type = typeStr switch
            {
                "inventory" => ImportType.Inventory,
                "sales" => ImportType.Sales,
                _ => ImportType.Partners
            };
            SetType(type);
        }
    }

    public async Task PickFileAsync()
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select Excel or CSV file",
                FileTypes = SpreadsheetTypes
            });

            if (result == null)
                return;

            await LoadFileAsync(result.FullPath);
        }
        catch (Exception ex)
        {
            FileError = $"Could not open file picker: {ex.Message}";
        }
    }

    public async Task LoadFileAsync(string path)
    {
        _selectedFile = path;
        FileName = Path.GetFileName(path);
        FileError = string.Empty;
        PreviewNote = string.Empty;
        Rows.Clear();

        var preview = await _importService.LoadPreviewAsync(path, _type);
        if (preview.HasError)
        {
            FileError = preview.Error;
            OnPropertyChanged(nameof(HasPreview));
            return;
        }

        PreviewNote = preview.PreviewNote;
        foreach (var row in preview.Rows)
            Rows.Add(row);

        OnPropertyChanged(nameof(HasPreview));
    }

    public async Task DownloadTemplateAsync()
    {
        try
        {
            var ms = await _importService.CreateTemplateAsync(_type);
            var cacheFile = Path.Combine(FileSystem.CacheDirectory, $"{_type.ToString().ToLowerInvariant()}_template.xlsx");

            await using var fs = File.Create(cacheFile);
            ms.CopyTo(fs);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = $"{ImportService.TitleFor(_type)} template",
                File = new ShareFile(cacheFile)
            });
        }
        catch (Exception ex)
        {
            FileError = $"Could not create template: {ex.Message}";
        }
    }

    public async Task ImportAsync()
    {
        if (string.IsNullOrWhiteSpace(_selectedFile) || IsImporting)
            return;

        IsImporting = true;
        OnPropertyChanged(nameof(HasPreview));
        try
        {
            var result = await _importService.ImportAsync(_selectedFile, _type);
            var message = result.Warnings.Count > 0
                ? $"Imported {result.RowsImported} row(s).\n\nWarnings:\n- {string.Join("\n- ", result.Warnings.Take(8))}"
                : $"Imported {result.RowsImported} row(s).";

            await Shell.Current.DisplayAlertAsync(ImportService.TitleFor(_type), message, "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Import Failed", ex.Message, "OK");
        }
        finally
        {
            IsImporting = false;
        }
    }
}