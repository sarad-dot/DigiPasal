using SQLite;

namespace DigiPasal.Models;

[Table("DayBookLogs")]
public class DayBookLog
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public DateTime BookDate { get; set; }

    public string Action { get; set; } = "Closed";

    public int UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; }

    public string Note { get; set; } = string.Empty;
}