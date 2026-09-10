using DigiPasal.Services;

namespace DigiPasal;

public partial class App : Application
{
    internal static Task StartupInitialization { get; private set; } = Task.CompletedTask;

    public App()
    {
        InitializeComponent();
        StartupInitialization = RunStartupInitializationAsync();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    private static async Task RunStartupInitializationAsync()
    {
        await DatabaseService.Instance.InitializeAsync();

        try
        {
            await BackupService.Instance.CreateBackupIfDueAsync(TimeSpan.FromHours(12));
        }
        catch
        {
            // A failed automatic backup must never block app startup.
        }
    }
}