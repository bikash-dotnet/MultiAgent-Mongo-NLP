using System.Globalization;
using System.Text;

namespace Gateway.Reports;

public static class NarrativeInsights
{
    public static BriefingResult Standard(
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows)
    {
        if (rows.Count == 0)
        {
            return new BriefingResult("No records matched the request.", false);
        }

        var builder = new StringBuilder();
        builder.Append($"This report contains {rows.Count} record{(rows.Count == 1 ? string.Empty : "s")}.");

        var numericColumn = FindNumericColumn(columns, rows);
        if (numericColumn is not null && TryMedian(rows, numericColumn, out var median))
        {
            builder.Append($" The median {numericColumn} is {median.ToString("0.##", CultureInfo.InvariantCulture)}.");
        }

        var marketColumn = columns.FirstOrDefault(column => column is "address.market" or "market");
        if (marketColumn is not null)
        {
            var groups = rows
                .Select(row => row.TryGetValue(marketColumn, out var value) ? value : null)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .GroupBy(value => value!, StringComparer.Ordinal)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .ToList();

            if (groups.Count > 0)
            {
                builder.Append($" The top market is {groups[0].Key} with {groups[0].Count()} records.");
            }
        }

        return new BriefingResult(builder.ToString(), false);
    }

    private static string? FindNumericColumn(
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows)
    {
        foreach (var column in columns)
        {
            var values = rows
                .Select(row => row.TryGetValue(column, out var value) ? value : null)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            if (values.Count > 0 && values.All(value => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _)))
            {
                return column;
            }
        }

        return null;
    }

    private static bool TryMedian(
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows,
        string column,
        out decimal median)
    {
        var values = rows
            .Select(row => row.TryGetValue(column, out var value) ? value : null)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => decimal.Parse(value!, NumberStyles.Number, CultureInfo.InvariantCulture))
            .OrderBy(value => value)
            .ToList();

        if (values.Count == 0)
        {
            median = 0;
            return false;
        }

        var middle = values.Count / 2;
        median = values.Count % 2 == 0
            ? (values[middle - 1] + values[middle]) / 2m
            : values[middle];
        return true;
    }
}
