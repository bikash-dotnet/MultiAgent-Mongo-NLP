using ClosedXML.Excel;
using Gateway.Reports;
using Gateway.Rules;

namespace Gateway.Tests.Reports;

public class XlsxExporterTests
{
    private static ColumnDefinition Def(string field, string? label = null) =>
        new(
            ColumnDefinition.NewId(),
            field,
            label ?? field,
            ColumnDataType.String,
            null,
            0,
            true,
            true,
            false,
            null,
            0,
            null,
            null);

    [Fact]
    public void Export_writes_headers_and_rows()
    {
        var rows = new List<IReadOnlyDictionary<string, string?>>
        {
            new Dictionary<string, string?> { ["name"] = "Alpha", ["price"] = "100" },
            new Dictionary<string, string?> { ["name"] = "Beta", ["price"] = "200" }
        };

        var bytes = XlsxExporter.Export(rows, [Def("name"), Def("price")]);

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);
        Assert.Equal("name", sheet.Cell(1, 1).GetString());
        Assert.Equal("price", sheet.Cell(1, 2).GetString());
        Assert.Equal("Alpha", sheet.Cell(2, 1).GetString());
        Assert.Equal("Beta", sheet.Cell(3, 1).GetString());
        Assert.Equal("200", sheet.Cell(3, 2).GetString());
    }

    [Fact]
    public void Export_produces_a_zip_container()
    {
        var bytes = XlsxExporter.Export([], [Def("name")]);

        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);
    }

    [Fact]
    public void Missing_cells_become_empty_strings()
    {
        var rows = new List<IReadOnlyDictionary<string, string?>>
        {
            new Dictionary<string, string?> { ["name"] = "Alpha" }
        };

        var bytes = XlsxExporter.Export(rows, [Def("name"), Def("price")]);

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        Assert.Equal(string.Empty, workbook.Worksheet(1).Cell(2, 2).GetString());
    }

    [Fact]
    public void Uses_column_label_as_header()
    {
        var rows = new List<IReadOnlyDictionary<string, string?>>
        {
            new Dictionary<string, string?> { ["name"] = "Alpha" }
        };

        var bytes = XlsxExporter.Export(rows, [Def("name", "Guest Name")]);

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);
        Assert.Equal("Guest Name", sheet.Cell(1, 1).GetString());
    }
}
