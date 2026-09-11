using SQLite;

namespace DigiPasal.Models;

[Table("Purchases")]
public class Purchase
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Unique = true)]
    public string BillNumber { get; set; } = string.Empty;

    public string WholesalerName { get; set; } = string.Empty;

    public DateTime BillDate { get; set; }

    public decimal TotalAmount { get; set; }

    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}