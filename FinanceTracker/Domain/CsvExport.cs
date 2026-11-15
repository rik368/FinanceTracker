using System.Globalization;
using System.Text;
using FinanceTracker.Data;

namespace FinanceTracker.Domain;

public static class CsvExport
{
    public static byte[] Create(IEnumerable<Transaction> transactions)
    {
        var text = new StringBuilder("Date,Category,Type,Amount KZT,Note\r\n");
        foreach (var entry in transactions)
        {
            text.Append(entry.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',');
            text.Append(Cell(entry.Category.Name)).Append(',');
            text.Append(entry.Category.Flow).Append(',');
            text.Append((entry.Cents / 100m).ToString("0.00", CultureInfo.InvariantCulture)).Append(',');
            text.Append(Cell(entry.Note)).Append("\r\n");
        }
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text.ToString())];
    }

    private static string Cell(string value)
    {
        // Spreadsheet applications may interpret user text as a formula.
        var trimmed = value.TrimStart();
        if (trimmed.Length > 0 && "=+-@".Contains(trimmed[0]))
            value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
