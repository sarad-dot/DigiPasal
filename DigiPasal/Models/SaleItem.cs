using SQLite;

namespace DigiPasal.Models;

[Table("SaleItems")]
public class SaleItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int SaleId { get; set; }

    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string Unit { get; set; } = "pcs";

    public decimal UnitPrice { get; set; }

    public double Quantity { get; set; }

    public decimal Discount { get; set; }

    public string DiscountType { get; set; } = "None";

    public decimal LineTotal { get; set; }
}