using System.Globalization;
using System.Text;

namespace HR28.Infrastructure.Helpers;

/// <summary>
/// Builds Excel-friendly CSV (UTF-8 with BOM so Dhivehi and other
/// non-Latin names open correctly).
/// </summary>
public class CsvBuilder
{
    private readonly StringBuilder _sb = new();

    public CsvBuilder Row(params object?[] cells)
    {
        _sb.AppendJoin(',', cells.Select(Format));
        _sb.Append("\r\n");
        return this;
    }

    public CsvBuilder Blank()
    {
        _sb.Append("\r\n");
        return this;
    }

    public byte[] ToBytes() =>
        Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(_sb.ToString()))
            .ToArray();

    private static string Format(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            decimal d => d.ToString("0.##", CultureInfo.InvariantCulture),
            double d => d.ToString("0.##", CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };

        // Stop spreadsheet apps treating a name like "=SUM(...)" as a formula.
        if (value is string && text.Length > 0 && "=+-@\t\r".Contains(text[0]))
            text = "'" + text;

        return text.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0
            ? "\"" + text.Replace("\"", "\"\"") + "\""
            : text;
    }
}
