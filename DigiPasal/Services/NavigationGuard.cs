using System.Threading;

namespace DigiPasal.Services;

/// <summary>
/// Prevents accidental duplicate or overlapping Shell navigation caused by
/// rapid repeated taps. Drops a navigation request while another one is still
/// in flight, and skips a relative push whose destination route is already the
/// route on top of the current navigation stack.
/// </summary>
public static class NavigationGuard
{
    private static int _navigating;

    /// <summary>
    /// Executes Shell navigation for UI-initiated navigation.
    /// </summary>
    public static async Task GoToAsync(string route)
    {
        if (Interlocked.CompareExchange(ref _navigating, 1, 0) != 0)
            return;

        try
        {
            if (IsRelativePush(route) && TopRouteEquals(route))
                return;

            await Shell.Current.GoToAsync(route);
        }
        finally
        {
            Interlocked.Exchange(ref _navigating, 0);
        }
    }

    private static bool IsRelativePush(string route)
    {
        if (string.IsNullOrWhiteSpace(route))
            return false;

        return route != ".." && !route.StartsWith("//", StringComparison.Ordinal);
    }

    private static bool TopRouteEquals(string route)
    {
        var location = Shell.Current.CurrentState.Location;
        if (location == null || string.IsNullOrWhiteSpace(location.OriginalString))
            return false;

        var segments = location.OriginalString.Split('/');
        var top = segments.Length > 0 ? segments[^1] : string.Empty;
        var target = route.Split('?')[0].Trim('/');
        return string.Equals(top, target, StringComparison.OrdinalIgnoreCase);
    }
}