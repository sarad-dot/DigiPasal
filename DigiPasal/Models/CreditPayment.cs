using SQLite;

namespace DigiPasal.Models;

[Table("CreditPayments")]
public class CreditPayment
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int CustomerId { get; set; }

    public int? SaleId { get; set; }

    public decimal Amount { get; set; }

    public int CreditBookType { get; set; }

    public bool IsTransfer { get; set; }

    public string Notes { get; set; } = string.Empty;

    [Indexed]
    public DateTime PaymentDate { get; set; }

    public DateTime CreatedAt { get; set; }
}