namespace DigiPasal.ViewModels;

/// <summary>
/// Cancels any pending invocation and schedules another after <paramref name="delayMs"/>.
/// Continuations run on the captured SynchronizationContext (UI thread), so the action
/// may safely touch ObservableCollections.
/// </summary>
public sealed class Debouncer
{
    private CancellationTokenSource? _cts;

    public void Debounce(int delayMs, Func<Task> action)
    {
        _cts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;
        _ = RunAsync(delayMs, cts.Token, action);
    }

    private static async Task RunAsync(int delayMs, CancellationToken token, Func<Task> action)
    {
        try
        {
            await Task.Delay(delayMs, token);
            await action();
        }
        catch (TaskCanceledException)
        {
        }
    }
}