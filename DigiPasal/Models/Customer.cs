using SQLite;

namespace DigiPasal.Models;

[Table("Customers")]
public class Customer
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Name { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public decimal CreditLimit { get; set; }

    public decimal DailyBalance { get; set; }

    public decimal PartnerBalance { get; set; }

    public decimal CurrentBalance { get; set; }

    [Indexed]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}