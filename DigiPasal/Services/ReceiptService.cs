using System.Text;
using DigiPasal.Models;

namespace DigiPasal.Services;

public class ReceiptService
{
    private static readonly Lazy<ReceiptService> _lazyInstance = new(() => new ReceiptService());

    public static ReceiptService Instance => _lazyInstance.Value;

    private ReceiptService() { }

    public async Task<string> GenerateReceiptTextAsync(Sale sale, List<SaleItem> items)
    {
        var shopName = await DatabaseService.Instance.GetSettingAsync("shop_name") ?? "DigiPasal";
        var shopAddress = await DatabaseService.Instance.GetSettingAsync("shop_address") ?? "";
        var shopPhone = await DatabaseService.Instance.GetSettingAsync("shop_phone") ?? "";

        var sb = new StringBuilder();

        sb.AppendLine(shopName.ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(shopAddress))
            sb.AppendLine(shopAddress);
        if (!string.IsNullOrWhiteSpace(shopPhone))
            sb.AppendLine(shopPhone);
        sb.AppendLine(new string('-', 32));
        sb.AppendLine($"Receipt: {sale.ReceiptNumber}");
        sb.AppendLine($"Date: {sale.CreatedAt.ToLocalTime():dd/MM/yyyy HH:mm}");
        sb.AppendLine(new string('-', 32));
        sb.AppendLine($"{"Item",-20} {"Qty",4} {"Rate",8} {"Total",8}");
        sb.AppendLine(new string('-', 32));

        foreach (var item in items)
        {
            var name = (item.ProductName ?? string.Empty);
            name = name.Length > 18 ? name[..17] + "." : name;
            var qty = FormatQuantity(item.Quantity, item.Unit);
            sb.AppendLine($"{name,-20} {qty,4} {CurrencyFormatter.Format(item.UnitPrice),8} {CurrencyFormatter.Format(item.LineTotal),8}");
        }

        sb.AppendLine(new string('-', 32));
        sb.AppendLine($"{"Subtotal:",-20} {CurrencyFormatter.Format(sale.Subtotal),12}");

        if (sale.DiscountAmount > 0)
            sb.AppendLine($"{"Discount:",-20} {CurrencyFormatter.Format(-sale.DiscountAmount),12}");

        if (sale.TaxAmount > 0)
            sb.AppendLine($"{"Tax:",-20} {CurrencyFormatter.Format(sale.TaxAmount),12}");

        sb.AppendLine(new string('=', 32));
        sb.AppendLine($"{"TOTAL:",-20} {CurrencyFormatter.Format(sale.GrandTotal),12}");
        sb.AppendLine(new string('=', 32));

        sb.AppendLine($"{"Paid:",-20} {CurrencyFormatter.Format(sale.AmountPaid),12}");

        if (sale.IsCredit && sale.CreditAmount > 0)
        {
            sb.AppendLine($"{"Due:",-20} {CurrencyFormatter.Format(sale.CreditAmount),12}");
            if (!string.IsNullOrWhiteSpace(sale.CustomerName))
                sb.AppendLine($"Customer: {sale.CustomerName}");
        }

        sb.AppendLine();
        sb.AppendLine("  Thank you! / धन्यवाद!");

        return sb.ToString();
    }

    public async Task<string> GenerateReceiptHtmlAsync(Sale sale, List<SaleItem> items)
    {
        var shopName = await DatabaseService.Instance.GetSettingAsync("shop_name") ?? "DigiPasal";
        var shopAddress = await DatabaseService.Instance.GetSettingAsync("shop_address") ?? "";
        var shopPhone = await DatabaseService.Instance.GetSettingAsync("shop_phone") ?? "";

        var sb = new StringBuilder();
        sb.AppendLine("<html><body style=\"font-family:monospace;padding:20px;\">");
        sb.AppendLine($"<h2 style=\"text-align:center;margin:0;\">{shopName}</h2>");
        if (!string.IsNullOrWhiteSpace(shopAddress))
            sb.AppendLine($"<p style=\"text-align:center;margin:2px 0;\">{shopAddress}</p>");
        if (!string.IsNullOrWhiteSpace(shopPhone))
            sb.AppendLine($"<p style=\"text-align:center;margin:2px 0;\">{shopPhone}</p>");
        sb.AppendLine("<hr/>");
        sb.AppendLine($"<p><b>Receipt:</b> {sale.ReceiptNumber}</p>");
        sb.AppendLine($"<p><b>Date:</b> {sale.CreatedAt.ToLocalTime():dd/MM/yyyy HH:mm}</p>");
        sb.AppendLine("<hr/>");
        sb.AppendLine("<table style=\"width:100%;border-collapse:collapse;\">");
        sb.AppendLine("<tr style=\"border-bottom:1px solid #ccc;\"><th align=\"left\">Item</th><th align=\"right\">Qty</th><th align=\"right\">Rate</th><th align=\"right\">Total</th></tr>");

        foreach (var item in items)
        {
            sb.AppendLine($"<tr><td>{item.ProductName ?? string.Empty}</td><td align=\"right\">{FormatQuantity(item.Quantity, item.Unit)}</td><td align=\"right\">{CurrencyFormatter.Format(item.UnitPrice)}</td><td align=\"right\">{CurrencyFormatter.Format(item.LineTotal)}</td></tr>");
        }

        sb.AppendLine("</table><hr/>");
        sb.AppendLine($"<p><b>Subtotal:</b> {CurrencyFormatter.Format(sale.Subtotal)}</p>");

        if (sale.DiscountAmount > 0)
            sb.AppendLine($"<p><b>Discount:</b> {CurrencyFormatter.Format(-sale.DiscountAmount)}</p>");

        if (sale.TaxAmount > 0)
            sb.AppendLine($"<p><b>Tax:</b> {CurrencyFormatter.Format(sale.TaxAmount)}</p>");

        sb.AppendLine($"<p style=\"font-size:1.2em;\"><b>TOTAL: {CurrencyFormatter.Format(sale.GrandTotal)}</b></p>");
        sb.AppendLine($"<p><b>Paid:</b> {CurrencyFormatter.Format(sale.AmountPaid)}</p>");

        if (sale.IsCredit && sale.CreditAmount > 0)
        {
            sb.AppendLine($"<p style=\"color:red;\"><b>Due: {CurrencyFormatter.Format(sale.CreditAmount)}</b></p>");
            if (!string.IsNullOrWhiteSpace(sale.CustomerName))
                sb.AppendLine($"<p><b>Customer:</b> {sale.CustomerName}</p>");
        }

        sb.AppendLine("<hr/>");
        sb.AppendLine("<p style=\"text-align:center;\">Thank you! / धन्यवाद!</p>");
        sb.AppendLine("</body></html>");

        return sb.ToString();
    }

    public async Task ShareReceiptAsync(Sale sale, List<SaleItem> items)
    {
        try
        {
            var receiptText = await GenerateReceiptTextAsync(sale, items);

            await Share.RequestAsync(new ShareTextRequest
            {
                Text = receiptText,
                Title = "Share Receipt"
            });
        }
        catch (FeatureNotSupportedException)
        {
            await Shell.Current.DisplayAlert("Not Supported", "Sharing is not supported on this device.", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to share receipt: {ex.Message}", "OK");
        }
    }

    private static string FormatQuantity(double quantity, string unit)
    {
        return (unit ?? "pcs").ToLowerInvariant() switch
        {
            "kg" or "ltr" or "m" => $"{quantity:0.##} {unit}",
            _ => quantity % 1 == 0 ? $"{(int)quantity} {unit}" : $"{quantity:0.##} {unit}"
        };
    }
}
