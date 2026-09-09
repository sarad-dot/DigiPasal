using System.Globalization;

namespace DigiPasal.Services;

public static class CurrencyFormatter
{
    public static string Format(decimal amount)
    {
        return $"रु {amount.ToString("N2", CultureInfo.InvariantCulture)}";
    }

    public static string Format(double amount)
    {
        return Format((decimal)amount);
    }
}