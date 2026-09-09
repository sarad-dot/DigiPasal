using System.Globalization;

namespace DigiPasal.Services;

public static class UnitsHelper
{
    public static string FormatQuantity(double quantity)
    {
        if (double.IsNaN(quantity) || double.IsInfinity(quantity))
            return "0";

        if (Math.Abs(quantity % 1) < 0.0000001 && quantity < 1_000_000_000)
            return ((long)Math.Round(quantity)).ToString(CultureInfo.InvariantCulture);

        return quantity.ToString("0.##", CultureInfo.InvariantCulture);
    }

    public static string FormatStock(double quantity, string unit)
    {
        string unitText = string.IsNullOrWhiteSpace(unit) ? "pcs" : unit;
        return $"{FormatQuantity(quantity)} {unitText}";
    }
}