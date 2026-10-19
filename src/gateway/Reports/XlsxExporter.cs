using ClosedXML.Excel;
using Gateway.Rules;

namespace Gateway.Reports;

public static class XlsxExporter
{
    public static byte[] Export(
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows,
        IReadOnlyList<ColumnDefinition> columns)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Report");

        for (var column = 0; column < columns.Count; column++)
        {
            sheet.Cell(1, column + 1).Value = columns[column].Label ?? columns[column].Field;
        }

        for (var row = 0; row < rows.Count; row++)
        {
            for (var column = 0; column < columns.Count; column++)
            {
                var field = columns[column].Field;
                var text = rows[row].TryGetValue(field, out var value)
                    ? value ?? string.Empty
                    : string.Empty;
                sheet.Cell(row + 2, column + 1).Value = text;
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
