using SQLite;

namespace DigiPasal.Models;

[Table("PurchaseItems")]
public class PurchaseItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int PurchaseId { get; set; }

    [Indexed]
    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public double Quantity { get; set; }

    public decimal CostPrice { get; set; }

    public decimal LineTotal { get; set; }
}