using SQLite;

namespace DigiPasal.Models;

[Table("Sales")]
public class Sale
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Unique = true)]
    public string ReceiptNumber { get; set; } = string.Empty;

    public decimal Subtotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal GrandTotal { get; set; }

    public decimal AmountPaid { get; set; }

    public decimal CreditAmount { get; set; }

    public string PaymentMethod { get; set; } = "Cash";

    public bool IsCredit { get; set; }

    [Indexed]
    public int? CustomerId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public int UserId { get; set; }

    public string Notes { get; set; } = string.Empty;

    public bool IsVoided { get; set; }

    [Indexed]
    public DateTime CreatedAt { get; set; }
}