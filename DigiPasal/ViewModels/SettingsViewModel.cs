using System.Collections.ObjectModel;
using System.Windows.Input;
using DigiPasal.Services;

namespace DigiPasal.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        private readonly BackupService _backupService;
        private readonly CreditService _creditService;

        private string _lastBackupTime = "No backups yet";
        private string _statusMessage = string.Empty;
        private bool _isRestoring;
        private BackupInfo? _selectedBackup;
        private bool _canReopenBook;
        private string _reopenBookHint = string.Empty;
        private string _shopName = "DigiPasal";
        private string _shopAddress = string.Empty;
        private string _shopPhone = string.Empty;

        public ObservableCollection<BackupInfo> Backups { get; } = new();

        public string LastBackupTime
        {
            get => _lastBackupTime;
            set => SetProperty(ref _lastBackupTime, value);
        }

        public bool CanReopenBook
        {
            get => _canReopenBook;
            set => SetProperty(ref _canReopenBook, value);
        }

        public string ReopenBookHint
        {
            get => _reopenBookHint;
            set => SetProperty(ref _reopenBookHint, value);
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

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (SetProperty(ref _statusMessage, value))
                    OnPropertyChanged(nameof(HasStatusMessage));
            }
        }

        public bool HasStatusMessage => !string.IsNullOrEmpty(_statusMessage);

        public bool HasNoBackups => Backups.Count == 0;

        public bool IsRestoring
        {
            get => _isRestoring;
            set => SetProperty(ref _isRestoring, value);
        }

        public BackupInfo? SelectedBackup
        {
            get => _selectedBackup;
            set => SetProperty(ref _selectedBackup, value);
        }

        public ICommand BackupNowCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand RestoreCommand { get; }

        public SettingsViewModel(BackupService backupService)
        {
            _backupService = backupService;
            _creditService = CreditService.Instance;
            Title = "Settings";

            BackupNowCommand = new Command(async () => await BackupNowAsync());
            RefreshCommand = new Command(async () => await LoadAsync());
            RestoreCommand = new Command<BackupInfo>(async (backup) => await RestoreAsync(backup));
        }

        public async Task LoadAsync()
        {
            IsBusy = true;
            try
            {
                await LoadShopInfoAsync();
                ReloadBackups();
                LastBackupTime = _backupService.GetLastBackupTime() ?? "No backups yet";
                await RefreshBookkeepingAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadShopInfoAsync()
        {
            try
            {
                var settings = await SettingService.Instance.GetShopSettingsAsync();
                ShopName = settings.ShopName;
                ShopAddress = settings.ShopAddress;
                ShopPhone = settings.ShopPhone;
            }
            catch
            {
                ShopName = "DigiPasal";
                ShopAddress = string.Empty;
                ShopPhone = string.Empty;
            }
        }

        public async Task RefreshBookkeepingAsync()
        {
            try
            {
                var lastClosed = await _creditService.GetLastClosedDateAsync();
                CanReopenBook = lastClosed.HasValue && lastClosed.Value.Date <= DateTime.Today;
                ReopenBookHint = lastClosed.HasValue
                    ? $"Reopens the day book for {NepaliDateConverter.Format(lastClosed.Value.Date)} so a backdated credit sale can be recorded"
                    : string.Empty;
            }
            catch
            {
                CanReopenBook = false;
                ReopenBookHint = string.Empty;
            }
        }

        public async Task ReopenDailyBookAsync()
        {
            if (!CanReopenBook)
                return;

            var lastClosed = await _creditService.GetLastClosedDateAsync();
            if (!lastClosed.HasValue)
            {
                CanReopenBook = false;
                return;
            }

            var confirm = await Shell.Current.DisplayAlertAsync(
                "Reopen Daily Book",
                $"Reopen the daily book for {NepaliDateConverter.Format(lastClosed.Value.Date)}? Transactions from the Partners book will be moved back under 'Closed' as a credit carry.",
                "Reopen", "Cancel");

            if (!confirm)
                return;

            try
            {
                var count = await _creditService.ReopenDailyBookAsync(lastClosed.Value.Date);
                await RefreshBookkeepingAsync();
                await Shell.Current.DisplayAlertAsync(
                    "Daily Book Reopened",
                    count > 0
                        ? $"The day book is open again. {count} transfer(s) were undone."
                        : "The day book is open again.",
                    "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error", $"Failed to reopen daily book: {ex.Message}", "OK");
            }
        }

        public async Task NavigateBookLogAsync()
        {
            await NavigationGuard.GoToAsync("DayBookLog");
        }

        public async Task NavigateImportAsync(string type)
        {
            await NavigationGuard.GoToAsync($"DataImport?type={type}");
        }

        public async Task NavigateWholesaleBillAsync()
        {
            await NavigationGuard.GoToAsync("WholesaleBill");
        }

        public async Task<bool> BackupNowAsync()
        {
            IsBusy = true;
            ClearError();
            try
            {
                bool success = await _backupService.CreateBackupAsync();
                if (success)
                {
                    StatusMessage = "Backup created successfully";
                    ReloadBackups();
                    LastBackupTime = _backupService.GetLastBackupTime() ?? LastBackupTime;
                }
                else
                {
                    SetError("Failed to create backup.");
                }

                return success;
            }
            catch (Exception ex)
            {
                SetError(ex.Message);
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task<bool> RestoreAsync(BackupInfo backup)
        {
            if (backup == null)
                return false;

            IsBusy = true;
            IsRestoring = true;
            ClearError();
            try
            {
                bool success = await _backupService.RestoreBackupAsync(backup.FilePath);
                if (success)
                {
                    ReloadBackups();
                    LastBackupTime = _backupService.GetLastBackupTime() ?? LastBackupTime;
                    StatusMessage = "Database restored. Please restart the app.";
                }
                else
                {
                    SetError("Failed to restore backup.");
                }

                return success;
            }
            catch (Exception ex)
            {
                SetError(ex.Message);
                return false;
            }
            finally
            {
                IsBusy = false;
                IsRestoring = false;
            }
        }

        private void ReloadBackups()
        {
            Backups.Clear();
            foreach (var backup in _backupService.GetAvailableBackups())
                Backups.Add(backup);

            OnPropertyChanged(nameof(HasNoBackups));
        }
    }
}