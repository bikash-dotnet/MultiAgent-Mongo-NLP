using Gateway.Reports;
using Gateway.Rules;

namespace Gateway.Tests.Reports;

public class CsvExporterTests
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
    public void Exports_only_the_requested_columns_with_a_header()
    {
        var rows = new List<ListingRow>
        {
            new("1", "Sunny \"Loft\"", "New York", 210m, "Entire home", 3, 4.8, "40.7", "-74.0")
        };

        var csv = CsvExporter.Export(rows, [Def("name"), Def("price")]);

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("name,price", lines[0]);
        Assert.Equal("\"Sunny \"\"Loft\"\"\",210", lines[1]);
    }

    [Fact]
    public void Unknown_columns_export_as_empty_values()
    {
        var rows = new List<ListingRow>
        {
            new("1", "Sunny Loft", "New York", 210m, "Entire home", 3, 4.8, null, null)
        };

        var csv = CsvExporter.Export(rows, [Def("name"), Def("security_deposit")]);

        Assert.Equal("name,security_deposit\nSunny Loft,", csv);
    }

    [Fact]
    public void Uses_column_label_as_header()
    {
        var rows = new List<ListingRow>
        {
            new("1", "Sunny Loft", "New York", 210m, "Entire home", 3, 4.8, "40.7", "-74.0")
        };

        var csv = CsvExporter.Export(rows, [Def("name", "Guest Name")]);

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Guest Name", lines[0]);
        Assert.Equal("Sunny Loft", lines[1]);
    }
}
