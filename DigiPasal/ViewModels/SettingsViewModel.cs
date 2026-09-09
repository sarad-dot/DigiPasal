using System.Collections.ObjectModel;
using System.Windows.Input;
using DigiPasal.Services;

namespace DigiPasal.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        private readonly BackupService _backupService;

        private string _lastBackupTime = "No backups yet";
        private string _statusMessage = string.Empty;
        private bool _isRestoring;
        private BackupInfo? _selectedBackup;

        public ObservableCollection<BackupInfo> Backups { get; } = new();

        public string LastBackupTime
        {
            get => _lastBackupTime;
            set => SetProperty(ref _lastBackupTime, value);
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
                ReloadBackups();
                LastBackupTime = _backupService.GetLastBackupTime() ?? "No backups yet";
            }
            finally
            {
                IsBusy = false;
            }
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