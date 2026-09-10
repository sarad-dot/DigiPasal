using System.Globalization;
using System.Text;

namespace DigiPasal.Services;

public static class CreditReportExportService
{
    public static async Task<bool> ExportAsync(string title, string fileName, string[] header, List<string[]> rows)
    {
        var sb = new StringBuilder();
        AppendCsvRow(sb, header);

        foreach (var row in rows)
            AppendCsvRow(sb, row);

        var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);

        try
        {
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = title,
                File = new ShareFile(filePath)
            });
            return true;
        }
        catch (FeatureNotSupportedException)
        {
            await Shell.Current.DisplayAlertAsync(
                "Not Supported", "Sharing is not supported on this device.", "OK");
            return false;
        }
    }

    private static void AppendCsvRow(StringBuilder sb, string[] fields)
    {
        for (var i = 0; i < fields.Length; i++)
        {
            if (i > 0)
                sb.Append(',');

            var field = fields[i] ?? string.Empty;
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                sb.Append('"');
                sb.Append(field.Replace("\"", "\"\""));
                sb.Append('"');
            }
            else
            {
                sb.Append(field);
            }
        }

        sb.AppendLine();
    }

    public static string FormatN(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);
}