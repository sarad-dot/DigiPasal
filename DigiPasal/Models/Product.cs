using SQLite;

namespace DigiPasal.Models;

[Table("Products")]
public class Product
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Name { get; set; } = string.Empty;

    [Indexed]
    public string Category { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal SellingPrice { get; set; }

    public decimal CostPrice { get; set; }

    public string Unit { get; set; } = "pcs";

    public double StockQuantity { get; set; }

    public double LowStockThreshold { get; set; }

    [Indexed]
    public string Barcode { get; set; } = string.Empty;

    public string SKU { get; set; } = string.Empty;

    [Indexed]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}