namespace DigiPasal.Services;

public class BackupInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }

    public string SizeDisplay => SizeBytes switch
    {
        < 1024 => $"{SizeBytes} B",
        < 1024 * 1024 => $"{SizeBytes / 1024.0:F1} KB",
        _ => $"{SizeBytes / (1024.0 * 1024.0):F1} MB"
    };

    public string CreatedAtDisplay => CreatedAt.ToLocalTime().ToString("MMM dd, yyyy  HH:mm");
}

public class BackupService
{
    private static readonly Lazy<BackupService> _lazyInstance = new(() => new BackupService());

    public static BackupService Instance => _lazyInstance.Value;

    private const int MaxBackups = 3;

    private readonly DatabaseService _dbService;

    private BackupService()
    {
        _dbService = DatabaseService.Instance;
    }

    public async Task<bool> CreateBackupAsync()
    {
        try
        {
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string backupFile = Path.Combine(_dbService.BackupsDirectory, $"digipasal_{timestamp}.db");

            if (!File.Exists(_dbService.DatabasePath))
                return false;

            await using var sourceStream = new FileStream(
                _dbService.DatabasePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            await using var destStream = new FileStream(
                backupFile, FileMode.Create, FileAccess.Write, FileShare.None);
            await sourceStream.CopyToAsync(destStream);

            TrimOldBackups();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a backup only if the newest regular backup is older than <paramref name="minAge"/>.
    /// Used by the startup path so cold starts skip the file copy on frequent launches.
    /// </summary>
    public async Task<bool> CreateBackupIfDueAsync(TimeSpan minAge)
    {
        var latest = GetAvailableBackups()
            .Where(b => !b.FileName.Contains("pre_restore"))
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefault();

        if (latest != null && DateTime.UtcNow - latest.CreatedAt < minAge)
            return false;

        return await CreateBackupAsync();
    }

    public async Task<bool> RestoreBackupAsync(string backupFilePath)
    {
        if (!File.Exists(backupFilePath))
            return false;

        try
        {
            await _dbService.CloseAsync();

            string safetyBackup = Path.Combine(
                _dbService.BackupsDirectory,
                $"digipasal_pre_restore_{DateTime.UtcNow:yyyyMMdd_HHmmss}.db");

            if (File.Exists(_dbService.DatabasePath))
                File.Copy(_dbService.DatabasePath, safetyBackup, overwrite: true);

            File.Copy(backupFilePath, _dbService.DatabasePath, overwrite: true);

            await _dbService.ReconnectAsync();

            return true;
        }
        catch
        {
            try
            {
                await _dbService.ReconnectAsync();
            }
            catch
            {
            }

            return false;
        }
    }

    public List<BackupInfo> GetAvailableBackups()
    {
        var backups = new List<BackupInfo>();

        if (!Directory.Exists(_dbService.BackupsDirectory))
            return backups;

        var files = Directory.GetFiles(_dbService.BackupsDirectory, "digipasal_*.db")
            .OrderByDescending(f => f)
            .ToList();

        foreach (var file in files)
        {
            var fileInfo = new FileInfo(file);
            backups.Add(new BackupInfo
            {
                FilePath = file,
                FileName = Path.GetFileName(file),
                SizeBytes = fileInfo.Length,
                CreatedAt = fileInfo.CreationTimeUtc
            });
        }

        return backups;
    }

    public string? GetLastBackupTime()
    {
        var backups = GetAvailableBackups();
        var latest = backups
            .Where(b => !b.FileName.Contains("pre_restore"))
            .FirstOrDefault();
        return latest?.CreatedAtDisplay;
    }

    private void TrimOldBackups()
    {
        if (!Directory.Exists(_dbService.BackupsDirectory))
            return;

        var regularBackups = Directory.GetFiles(_dbService.BackupsDirectory, "digipasal_*.db")
            .Where(f => !Path.GetFileName(f).Contains("pre_restore"))
            .OrderByDescending(f => f)
            .ToList();

        foreach (var old in regularBackups.Skip(MaxBackups))
        {
            try { File.Delete(old); }
            catch { }
        }
    }
}
