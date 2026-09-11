using System.Globalization;
using System.Text;
using DigiPasal.Models;
using MiniExcelLibs;
using SQLite;

namespace DigiPasal.Services;

public enum ImportType
{
    Partners,
    Inventory,
    Sales
}

public class ImportRow
{
    public int RowNumber { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;

    public bool HasError => !string.IsNullOrWhiteSpace(Error);
}

public class ImportPreview
{
    public string FileName { get; set; } = string.Empty;
    public List<string> Headers { get; set; } = new();
    public List<ImportRow> Rows { get; set; } = new();
    public int TotalRows { get; set; }
    public int RowLimit { get; set; } = 200;
    public string Error { get; set; } = string.Empty;

    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool HasData => Rows.Count > 0;

    public string PreviewNote => TotalRows > RowLimit
        ? $"Showing first {Rows.Count} of {TotalRows} data rows"
        : $"{TotalRows} data rows found";
}

public class ImportResult
{
    public int RowsImported { get; set; }
    public int SalesCreated { get; set; }
    public int ItemsImported { get; set; }
    public int ProductsCreated { get; set; }
    public int ProductsMerged { get; set; }
    public int CustomersCreated { get; set; }
    public int CustomersUpdated { get; set; }
    public List<string> Warnings { get; } = new();

    public string SuccessSummary
    {
        get
        {
            var parts = new List<string> { $"{RowsImported} row(s) imported" };
            if (SalesCreated > 0) parts.Add($"{SalesCreated} sale(s)");
            if (ItemsImported > 0) parts.Add($"{ItemsImported} line item(s)");
            if (ProductsCreated > 0) parts.Add($"{ProductsCreated} product(s) created");
            if (ProductsMerged > 0) parts.Add($"{ProductsMerged} product(s) merged/updated");
            if (CustomersCreated > 0) parts.Add($"{CustomersCreated} customer(s) created");
            if (CustomersUpdated > 0) parts.Add($"{CustomersUpdated} customer(s) updated");
            return string.Join(" · ", parts);
        }
    }
}

/// <summary>
/// Imports partners, inventory and past sales from .xlsx or .csv files.
/// Preview first (LoadPreviewAsync), then ImportAsync performs a single
/// transaction that either commits everything or rolls back completely.
/// </summary>
public class ImportService
{
    private static readonly Lazy<ImportService> _lazyInstance = new(() => new ImportService());

    public static ImportService Instance => _lazyInstance.Value;

    private readonly DatabaseService _dbService;

    private ImportService()
    {
        _dbService = DatabaseService.Instance;
    }

    private SQLiteAsyncConnection Database => _dbService.Database;

    public static string[] HeadersFor(ImportType type) => type switch
    {
        ImportType.Partners => new[] { "Name", "Phone", "Address", "CreditLimit", "OpeningBalance" },
        ImportType.Inventory => new[] { "Name", "Category", "Barcode", "SKU", "Unit", "SellingPrice", "CostPrice", "Stock", "LowStockThreshold" },
        ImportType.Sales => new[] { "SaleDate", "ReceiptNo", "CustomerName", "ProductName", "Barcode", "Quantity", "UnitPrice", "Discount", "PaymentType", "Book", "Notes" },
        _ => Array.Empty<string>()
    };

    public static string TitleFor(ImportType type) => type switch
    {
        ImportType.Partners => "Import Partners",
        ImportType.Inventory => "Import Inventory",
        ImportType.Sales => "Import Sales",
        _ => "Import"
    };

    public static string HintFor(ImportType type) => type switch
    {
        ImportType.Partners => "Columns: Name*, Phone, Address, CreditLimit, OpeningBalance",
        ImportType.Inventory => "Columns: Name*, Category, Barcode, SKU, Unit, SellingPrice, CostPrice, Stock, LowStockThreshold",
        ImportType.Sales => "Columns: SaleDate*, ReceiptNo, CustomerName, ProductName*, Barcode, Quantity*, UnitPrice*, Discount, PaymentType (Cash|Credit), Book (Daily|Partner), Notes",
        _ => string.Empty
    };

    public async Task<MemoryStream> CreateTemplateAsync(ImportType type)
    {
        var rows = type switch
        {
            ImportType.Partners => new List<Dictionary<string, object>>
            {
                new() { ["Name"] = "Ramesh & Sons", ["Phone"] = "98xxxxxxx", ["Address"] = "Kathmandu", ["CreditLimit"] = 50000m, ["OpeningBalance"] = 12500m }
            },
            ImportType.Inventory => new List<Dictionary<string, object>>
            {
                new() { ["Name"] = "Stapler", ["Category"] = "Stationery", ["Barcode"] = "", ["SKU"] = "STP-01", ["Unit"] = "pcs", ["SellingPrice"] = 450m, ["CostPrice"] = 380m, ["Stock"] = 50d, ["LowStockThreshold"] = 5d }
            },
            ImportType.Sales => new List<Dictionary<string, object>>
            {
                new() { ["SaleDate"] = "2080-05-12", ["ReceiptNo"] = "", ["CustomerName"] = "", ["ProductName"] = "Notebook", ["Barcode"] = "", ["Quantity"] = 2d, ["UnitPrice"] = 120m, ["Discount"] = 0m, ["PaymentType"] = "Cash", ["Book"] = "Daily", ["Notes"] = "" }
            },
            _ => new List<Dictionary<string, object>>()
        };

        var ms = new MemoryStream();
        await MiniExcel.SaveAsAsync(ms, rows);
        ms.Position = 0;
        return ms;
    }

    public async Task<ImportPreview> LoadPreviewAsync(string path, ImportType type)
    {
        var preview = new ImportPreview { FileName = Path.GetFileName(path) };

        try
        {
            var (headers, allRows) = await ReadAllRowsAsync(path);
            preview.Headers = headers.Select(h => h.Trim()).ToList();

            if (headers.Length == 0 || allRows.Count == 0)
            {
                preview.Error = "The file has no data rows.";
                return preview;
            }

            var col = BuildColumnIndex(headers);
            var required = RequiredColumns(type);
            var missing = required.Where(r => !col.ContainsKey(r)).ToList();
            if (missing.Count > 0)
            {
                preview.Error = $"Missing required column(s): {string.Join(", ", missing)}";
                return preview;
            }

            preview.TotalRows = allRows.Count;
            var shown = allRows.Take(preview.RowLimit).ToList();

            foreach (var raw in shown)
            {
                var row = ParsePreviewRaw(type, raw, col, shown.IndexOf(raw) + 2);
                preview.Rows.Add(row);
            }
        }
        catch (Exception ex)
        {
            preview.Error = $"Could not read file: {ex.Message}";
        }

        return preview;
    }

    public async Task<ImportResult> ImportAsync(string path, ImportType type)
    {
        var result = new ImportResult();

        var (headers, allRows) = await ReadAllRowsAsync(path);
        if (headers.Length == 0 || allRows.Count == 0)
        {
            result.Warnings.Add("The file has no data rows.");
            return result;
        }

        var col = BuildColumnIndex(headers);
        var required = RequiredColumns(type);
        var missing = required.Where(r => !col.ContainsKey(r)).ToArray();
        if (missing.Length > 0)
        {
            result.Warnings.Add($"Missing required column(s): {string.Join(", ", missing)}");
            return result;
        }

        int fileRow = 2;
        try
        {
            await Database.RunInTransactionAsync(tran =>
            {
                var products = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in tran.Table<Product>().ToList())
                {
                    products[KeyOf(p.Barcode, p.SKU, p.Name)] = p;
                }

                var customers = new Dictionary<string, Customer>(StringComparer.OrdinalIgnoreCase);
                foreach (var c in tran.Table<Customer>().ToList())
                    customers[NameKey(c.Name)] = c;

                switch (type)
                {
                    case ImportType.Partners:
                        ImportPartnersCore(tran, allRows, col, products, customers, result, ref fileRow);
                        break;
                    case ImportType.Inventory:
                        ImportInventoryCore(tran, allRows, col, products, result, ref fileRow);
                        break;
                    case ImportType.Sales:
                        ImportSalesCore(tran, allRows, col, products, customers, result, ref fileRow);
                        break;
                }
            });
        }
        catch (Exception ex)
        {
            result.Warnings.Clear();
            result.Warnings.Add($"Import failed: {ex.Message}");
        }

        return result;
    }

    // ─────────────────────────── PARTNERS ───────────────────────────

    private static void ImportPartnersCore(SQLiteConnection tran, List<string[]> rows, Dictionary<string, int> col,
        Dictionary<string, Product> products, Dictionary<string, Customer> customers, ImportResult result, ref int fileRow)
    {
        foreach (var raw in rows)
        {
            var curRow = fileRow;
            fileRow++;

            var name = GetStr(raw, col, "NAME");
            var phone = GetStr(raw, col, "PHONE");
            var address = GetStr(raw, col, "ADDRESS");

            if (string.IsNullOrWhiteSpace(name))
            {
                result.Warnings.Add($"Row {curRow}: missing Name, skipped");
                continue;
            }

            var creditLimit = GetDec(raw, col, "CREDITLIMIT");
            var opening = GetDec(raw, col, "OPENINGBALANCE");

            if (customers.TryGetValue(NameKey(name), out var customer))
            {
                if (creditLimit > 0)
                    customer.CreditLimit = creditLimit;
                if (!string.IsNullOrWhiteSpace(phone))
                    customer.Phone = phone;
                if (!string.IsNullOrWhiteSpace(address))
                    customer.Address = address;
                customer.PartnerBalance += opening;
                customer.CurrentBalance = customer.DailyBalance + customer.PartnerBalance;
                customer.UpdatedAt = DateTime.UtcNow;
                tran.Update(customer);
                result.CustomersUpdated++;
            }
            else
            {
                var created = new Customer
                {
                    Name = name.Trim(),
                    Phone = phone,
                    Address = address,
                    CreditLimit = creditLimit,
                    DailyBalance = 0,
                    PartnerBalance = opening,
                    CurrentBalance = opening,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                tran.Insert(created);
                customers[NameKey(created.Name)] = created;
                result.CustomersCreated++;
            }

            result.RowsImported++;
        }
    }

    // ─────────────────────────── INVENTORY ───────────────────────────

    private static void ImportInventoryCore(SQLiteConnection tran, List<string[]> rows, Dictionary<string, int> col,
        Dictionary<string, Product> products, ImportResult result, ref int fileRow)
    {
        foreach (var raw in rows)
        {
            var curRow = fileRow;
            fileRow++;

            var name = GetStr(raw, col, "NAME");
            if (string.IsNullOrWhiteSpace(name))
            {
                result.Warnings.Add($"Row {curRow}: missing Name, skipped");
                continue;
            }

            var barcode = GetStr(raw, col, "BARCODE");
            var sku = GetStr(raw, col, "SKU");
            var category = GetStr(raw, col, "CATEGORY");
            var unit = string.IsNullOrWhiteSpace(GetStr(raw, col, "UNIT")) ? "pcs" : GetStr(raw, col, "UNIT");
            var selling = GetDec(raw, col, "SELLINGPRICE");
            var cost = GetDec(raw, col, "COSTPRICE");
            var stock = GetDbl(raw, col, "STOCK");
            var low = GetDbl(raw, col, "LOWSTOCKTHRESHOLD");

            var matchKey = KeyOf(barcode, sku, name);
            if (products.TryGetValue(matchKey, out var existing))
            {
                if (selling >= 0)
                    existing.SellingPrice = selling;
                if (cost >= 0)
                    existing.CostPrice = cost;
                if (stock >= 0)
                    existing.StockQuantity += stock;
                if (low >= 0)
                    existing.LowStockThreshold = low;
                if (!string.IsNullOrWhiteSpace(barcode) && string.IsNullOrWhiteSpace(existing.Barcode))
                    existing.Barcode = barcode;
                if (!string.IsNullOrWhiteSpace(sku) && string.IsNullOrWhiteSpace(existing.SKU))
                    existing.SKU = sku;
                existing.Unit = unit;
                existing.UpdatedAt = DateTime.UtcNow;
                tran.Update(existing);
                result.ProductsMerged++;
            }
            else
            {
                var product = new Product
                {
                    Name = name.Trim(),
                    Category = category,
                    Barcode = barcode,
                    SKU = sku,
                    Unit = unit,
                    SellingPrice = Math.Max(0, selling),
                    CostPrice = Math.Max(0, cost),
                    StockQuantity = Math.Max(0, stock),
                    LowStockThreshold = Math.Max(0, low),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                tran.Insert(product);
                products[KeyOf(product.Barcode, product.SKU, product.Name)] = product;
                result.ProductsCreated++;
            }

            result.RowsImported++;
        }
    }

    // ─────────────────────────── SALES ───────────────────────────

    private sealed class ImportedLine
    {
        public DateTime SaleDate { get; init; }
        public string ReceiptNo { get; init; } = string.Empty;
        public string CustomerName { get; init; } = string.Empty;
        public string PaymentType { get; init; } = "Cash";
        public CreditBookType Book { get; init; }
        public string Notes { get; init; } = string.Empty;
        public int RowNumber { get; init; }
        public string ProductName { get; init; } = string.Empty;
        public string Barcode { get; init; } = string.Empty;
        public double Quantity { get; init; }
        public decimal UnitPrice { get; init; }
        public decimal Discount { get; init; }
    }

    private static void ImportSalesCore(SQLiteConnection tran, List<string[]> rows, Dictionary<string, int> col,
        Dictionary<string, Product> products, Dictionary<string, Customer> customers, ImportResult result, ref int fileRow)
    {
        var groups = new List<List<ImportedLine>>();
        var receiptToGroup = new Dictionary<string, List<ImportedLine>>(StringComparer.OrdinalIgnoreCase);
        var nextAutoReceipt = new Dictionary<DateTime, int>();

        foreach (var raw in rows)
        {
            var curRow = fileRow;
            fileRow++;

            if (!TryGetDate(raw, col, out var saleDate))
            {
                result.Warnings.Add($"Row {curRow}: invalid or missing SaleDate, skipped");
                continue;
            }
            saleDate = saleDate.Date;

            var productName = GetStr(raw, col, "PRODUCTNAME");
            var barcode = GetStr(raw, col, "BARCODE");
            if (string.IsNullOrWhiteSpace(productName) && string.IsNullOrWhiteSpace(barcode))
            {
                result.Warnings.Add($"Row {curRow}: missing ProductName/Barcode, skipped");
                continue;
            }

            var qty = GetDbl(raw, col, "QUANTITY");
            var price = GetDec(raw, col, "UNITPRICE");
            if (qty <= 0 || price < 0)
            {
                result.Warnings.Add($"Row {curRow}: Quantity must be > 0 and UnitPrice >= 0, skipped");
                continue;
            }

            var payment = NormalizePaymentType(GetStr(raw, col, "PAYMENTTYPE"));
            var book = NormalizeBook(GetStr(raw, col, "BOOK"));
            var receiptNo = GetStr(raw, col, "RECEIPTNO");
            var customerName = GetStr(raw, col, "CUSTOMERNAME");
            var discount = GetDec(raw, col, "DISCOUNT");
            var notes = GetStr(raw, col, "NOTES");

            var line = new ImportedLine
            {
                SaleDate = saleDate,
                ReceiptNo = receiptNo,
                CustomerName = customerName,
                PaymentType = payment,
                Book = book,
                Notes = notes,
                RowNumber = curRow,
                ProductName = productName,
                Barcode = barcode,
                Quantity = qty,
                UnitPrice = price,
                Discount = discount
            };

            if (!string.IsNullOrWhiteSpace(receiptNo))
            {
                if (!receiptToGroup.TryGetValue(receiptNo, out var group))
                {
                    group = new List<ImportedLine>();
                    receiptToGroup[receiptNo] = group;
                    groups.Add(group);
                }
                group.Add(line);
            }
            else
            {
                if (!nextAutoReceipt.ContainsKey(saleDate))
                    nextAutoReceipt[saleDate] = 1;
                var seq = nextAutoReceipt[saleDate]++;
                var single = new List<ImportedLine>
                {
                    new ImportedLine
                    {
                        SaleDate = line.SaleDate,
                        ReceiptNo = $"IMP-{saleDate:yyyyMMdd}-{seq:D4}",
                        CustomerName = line.CustomerName,
                        PaymentType = line.PaymentType,
                        Book = line.Book,
                        Notes = line.Notes,
                        RowNumber = line.RowNumber,
                        ProductName = line.ProductName,
                        Barcode = line.Barcode,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        Discount = line.Discount
                    }
                };
                groups.Add(single);
            }
        }

        var usedReceipts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            if (group.Count == 0)
                continue;

            var line0 = group[0];
            var receipt = line0.ReceiptNo;
            while (usedReceipts.Contains(receipt))
                receipt = $"IMP-{line0.SaleDate:yyyyMMdd}-{Guid.NewGuid():N}".Substring(0, 24);
            usedReceipts.Add(receipt);

            var isCredit = string.Equals(line0.PaymentType, "Credit", StringComparison.OrdinalIgnoreCase);
            var customerName = isCredit ? line0.CustomerName : string.Empty;

            int? customerId = null;
            if (isCredit)
            {
                if (string.IsNullOrWhiteSpace(customerName))
                {
                    result.Warnings.Add($"Row {line0.RowNumber}: Credit sale needs a CustomerName, skipped");
                    continue;
                }

                if (customers.TryGetValue(NameKey(customerName), out var customer))
                {
                    customerId = customer.Id;
                }
                else
                {
                    var created = new Customer
                    {
                        Name = customerName.Trim(),
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    tran.Insert(created);
                    customers[NameKey(created.Name)] = created;
                    customerId = created.Id;
                    result.CustomersCreated++;
                }
            }

            var subtotal = group.Sum(l => l.UnitPrice * (decimal)l.Quantity);
            var discount = group.Sum(l => l.Discount);
            var total = subtotal - discount;
            var creditAmount = isCredit ? total : 0m;
            var paid = isCredit ? 0m : total;

            var sale = new Sale
            {
                ReceiptNumber = receipt,
                Subtotal = subtotal,
                DiscountAmount = discount,
                TaxAmount = 0,
                TotalAmount = total,
                GrandTotal = total,
                AmountPaid = paid,
                CreditAmount = creditAmount,
                PaymentMethod = isCredit ? "Credit" : "Cash",
                IsCredit = isCredit,
                CreditBookType = (int)line0.Book,
                CustomerId = customerId,
                CustomerName = string.IsNullOrWhiteSpace(customerName) ? "Walk-in" : customerName.Trim(),
                UserId = 0,
                Notes = line0.Notes,
                IsVoided = false,
                IsQuickSale = false,
                CreatedAt = line0.SaleDate.ToUniversalTime()
            };
            tran.Insert(sale);

            foreach (var l in group)
            {
                var item = new SaleItem
                {
                    SaleId = sale.Id,
                    ProductName = l.ProductName,
                    Unit = "pcs",
                    UnitPrice = l.UnitPrice,
                    Quantity = l.Quantity,
                    Discount = l.Discount,
                    DiscountType = "Amount",
                    LineTotal = (l.UnitPrice * (decimal)l.Quantity) - l.Discount
                };

                Product? product = null;
                var pkey = KeyOf(l.Barcode, string.Empty, l.ProductName);
                if (products.TryGetValue(pkey, out product))
                {
                    item.ProductId = product.Id;
                    product.StockQuantity -= l.Quantity;
                    if (product.StockQuantity < 0)
                        product.StockQuantity = 0;
                    product.UpdatedAt = DateTime.UtcNow;
                    tran.Update(product);
                }
                else
                {
                    var created = new Product
                    {
                        Name = string.IsNullOrWhiteSpace(l.ProductName) ? "(imported)" : l.ProductName.Trim(),
                        Barcode = l.Barcode,
                        Unit = "pcs",
                        StockQuantity = 0,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    tran.Insert(created);
                    products[KeyOf(created.Barcode, created.SKU, created.Name)] = created;
                    item.ProductId = created.Id;
                    result.ProductsCreated++;
                    result.Warnings.Add($"Row {l.RowNumber}: product '{created.Name}' not found, created with no stock");
                }

                tran.Insert(item);
                result.ItemsImported++;
            }

            if (isCredit && customerId.HasValue && creditAmount > 0 && customers.TryGetValue(NameKey(sale.CustomerName), out var cust))
            {
                if (sale.CreditBookType == (int)CreditBookType.Partner)
                    cust.PartnerBalance += creditAmount;
                else
                    cust.DailyBalance += creditAmount;
                cust.CurrentBalance = cust.DailyBalance + cust.PartnerBalance;
                cust.UpdatedAt = DateTime.UtcNow;
                tran.Update(cust);
                result.CustomersUpdated++;
            }

            result.SalesCreated++;
            result.RowsImported++;
        }
    }

    // ─────────────────────────── READING ───────────────────────────

    private static async Task<(string[] headers, List<string[]> rows)> ReadAllRowsAsync(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();

        if (ext == ".csv")
            return ReadCsvRows(path);

        if (ext == ".xlsx" || ext == ".xls")
            return await ReadExcelRowsAsync(path);

        throw new InvalidOperationException("Unsupported file type. Use .xlsx or .csv.");
    }

    private static async Task<(string[] headers, List<string[]> rows)> ReadExcelRowsAsync(string path)
    {
        using var stream = File.OpenRead(path);
        var dynamicRows = MiniExcel.Query(stream).ToList();

        if (dynamicRows.Count == 0)
            return (Array.Empty<string>(), new List<string[]>());

        var first = dynamicRows[0];
        var keys = new List<string>();
        if (first is IDictionary<string, object> dict)
            keys = dict.Keys.ToList();

        var headers = keys.Select(k => k.Trim()).ToArray();
        var rows = new List<string[]>();

        foreach (var item in dynamicRows)
        {
            if (item is not IDictionary<string, object> row)
                continue;

            var values = new string[headers.Length];
            for (int i = 0; i < keys.Count; i++)
                values[i] = CellToString(row.TryGetValue(keys[i], out var v) ? v : null);
            rows.Add(values);
        }

        return await Task.FromResult((headers, rows));
    }

    private static string CellToString(object? value)
    {
        if (value == null)
            return string.Empty;
        if (value is DateTime dt)
            return dt.Date == dt ? dt.ToString("yyyy-MM-dd") : dt.ToString("yyyy-MM-dd HH:mm");
        if (value is double d)
            return d.ToString("0.############", CultureInfo.InvariantCulture);
        if (value is decimal m)
            return m.ToString(CultureInfo.InvariantCulture);
        if (value is int i)
            return i.ToString(CultureInfo.InvariantCulture);
        if (value is long l)
            return l.ToString(CultureInfo.InvariantCulture);
        return value.ToString()?.Trim() ?? string.Empty;
    }

    private static (string[] headers, List<string[]> rows) ReadCsvRows(string path)
    {
        var lines = File.ReadAllLines(path);
        var parsed = new List<List<string>>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            parsed.Add(ParseCsvLine(line));
        }

        if (parsed.Count == 0)
            return (Array.Empty<string>(), new List<string[]>());

        var headers = parsed[0].Select(h => h.Trim()).ToArray();
        var rows = parsed.Skip(1).Select(r => r.ToArray()).ToList();
        return (headers, rows);
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    sb.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        result.Add(sb.ToString());
        return result;
    }

    // ─────────────────────────── HELPERS ───────────────────────────

    private static Dictionary<string, int> BuildColumnIndex(string[] headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
        {
            var key = headers[i].Trim().ToUpperInvariant();
            key = key.Replace(" ", string.Empty).Replace("_", string.Empty);
            if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key))
                map[key] = i;
        }
        return map;
    }

    private static string[] RequiredColumns(ImportType type) => type switch
    {
        ImportType.Partners => new[] { "NAME" },
        ImportType.Inventory => new[] { "NAME" },
        ImportType.Sales => new[] { "SALEDATE", "PRODUCTNAME", "BARCODE", "QUANTITY", "UNITPRICE" },
        _ => Array.Empty<string>()
    };

    private static string GetStr(string[] raw, Dictionary<string, int> col, string key)
        => col.TryGetValue(key, out var idx) && idx < raw.Length
            ? (raw[idx] ?? string.Empty).Trim()
            : string.Empty;

    private static decimal GetDec(string[] raw, Dictionary<string, int> col, string key)
    {
        var s = GetStr(raw, col, key).Replace(",", string.Empty).Trim('₹', ' ', 'N', 'R', 's');
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ||
            decimal.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out d))
            return d;
        return 0;
    }

    private static double GetDbl(string[] raw, Dictionary<string, int> col, string key)
    {
        var s = GetStr(raw, col, key).Replace(",", string.Empty).Trim('₹', ' ', 'N', 'R', 's');
        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ||
            double.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out d))
            return d;
        return 0;
    }

    private static bool TryGetDate(string[] raw, Dictionary<string, int> col, out DateTime date)
    {
        date = default;
        var s = GetStr(raw, col, "SALEDATE");

        if (string.IsNullOrWhiteSpace(s))
            return false;

        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
        {
            date = dt.Date;
            return true;
        }

        if (NepaliDateConverter.TryParseAny(s, out var nepali))
        {
            date = nepali.Date;
            return true;
        }

        return false;
    }

    private static string NormalizePaymentType(string raw)
    {
        var s = raw.Trim();
        if (string.IsNullOrWhiteSpace(s))
            return "Cash";
        var lower = s.ToLowerInvariant();
        if (lower.Contains("credit") || lower.Contains("udhar"))
            return "Credit";
        return "Cash";
    }

    private static CreditBookType NormalizeBook(string raw)
    {
        var s = raw.Trim();
        if (s.Equals("Partner", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("Partner Book", StringComparison.OrdinalIgnoreCase) ||
            s.ToLowerInvariant().Contains("partner"))
            return CreditBookType.Partner;
        return CreditBookType.Daily;
    }

    private static string NameKey(string name) => (name ?? string.Empty).Trim().ToUpperInvariant();

    private static string KeyOf(string barcode, string sku, string name)
    {
        if (!string.IsNullOrWhiteSpace(barcode))
            return "B:" + barcode.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(sku))
            return "S:" + sku.Trim().ToUpperInvariant();
        return "N:" + NameKey(name);
    }

    private static ImportRow ParsePreviewRaw(ImportType type, string[] raw, Dictionary<string, int> col, int rowNumber)
    {
        var row = new ImportRow { RowNumber = rowNumber };

        switch (type)
        {
            case ImportType.Partners:
                row.Summary = GetStr(raw, col, "NAME");
                row.Detail = string.Join(" · ", new[]
                {
                    GetStr(raw, col, "PHONE"),
                    "Limit " + CurrencyFormatter.Format(GetDec(raw, col, "CREDITLIMIT")),
                    "Opening " + CurrencyFormatter.Format(GetDec(raw, col, "OPENINGBALANCE"))
                }.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (string.IsNullOrWhiteSpace(row.Summary))
                    row.Error = "Missing Name";
                break;

            case ImportType.Inventory:
                row.Summary = GetStr(raw, col, "NAME");
                row.Detail = string.Join(" · ", new[]
                {
                    GetStr(raw, col, "BARCODE"),
                    GetStr(raw, col, "SKU"),
                    "Price " + CurrencyFormatter.Format(GetDec(raw, col, "SELLINGPRICE")),
                    "Cost " + CurrencyFormatter.Format(GetDec(raw, col, "COSTPRICE")),
                    "Stock " + GetDbl(raw, col, "STOCK").ToString("0.##")
                }.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (string.IsNullOrWhiteSpace(row.Summary))
                    row.Error = "Missing Name";
                break;

            case ImportType.Sales:
                var hasDate = TryGetDate(raw, col, out var parsed);
                row.Summary = GetStr(raw, col, "PRODUCTNAME") ?? "—";
                var qty = GetDbl(raw, col, "QUANTITY");
                var price = GetDec(raw, col, "UNITPRICE");
                row.Detail = string.Join(" · ", new[]
                {
                    hasDate ? NepaliDateConverter.FormatShort(parsed) : string.Empty,
                    GetStr(raw, col, "RECEIPTNO"),
                    GetStr(raw, col, "CUSTOMERNAME"),
                    $"{qty:0.##} x {price:0.##}",
                    GetStr(raw, col, "PAYMENTTYPE"),
                    GetStr(raw, col, "BOOK")
                }.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (!hasDate)
                    row.Error = "Invalid SaleDate";
                else if (qty <= 0 || price < 0)
                    row.Error = "Quantity > 0, UnitPrice >= 0";
                break;
        }

        return row;
    }
}