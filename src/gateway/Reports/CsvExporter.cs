using System.Globalization;
using System.Text;

namespace Gateway.Reports;

public static class CsvExporter
{
    private static readonly Dictionary<string, Func<ListingRow, string>> Selectors = new(StringComparer.Ordinal)
    {
        ["name"] = row => row.Name,
        ["address.market"] = row => row.Market,
        ["price"] = row => row.Price.ToString(CultureInfo.InvariantCulture),
        ["room_type"] = row => row.RoomType,
        ["accommodates"] = row => row.Accommodates.ToString(CultureInfo.InvariantCulture),
        ["review_scores.rating"] = row => row.Rating.ToString("0.0", CultureInfo.InvariantCulture),
        ["address.location.coordinates"] = row =>
            row.Latitude is null ? string.Empty : $"{row.Latitude},{row.Longitude}"
    };

    public static string Export(IReadOnlyList<ListingRow> rows, IReadOnlyList<string> columns)
    {
        var builder = new StringBuilder();
        builder.Append(string.Join(',', columns.Select(Escape)));

        foreach (var row in rows)
        {
            builder.Append('\n');
            builder.Append(string.Join(',', columns.Select(column => Escape(Value(row, column)))));
        }

        return builder.ToString();
    }

    private static string Value(ListingRow row, string column)
    {
        return Selectors.TryGetValue(column, out var selector) ? selector(row) : string.Empty;
    }

    private static string Escape(string value)
    {
        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
