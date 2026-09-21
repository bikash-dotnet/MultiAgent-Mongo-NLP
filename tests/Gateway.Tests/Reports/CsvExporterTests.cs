using Gateway.Reports;

namespace Gateway.Tests.Reports;

public class CsvExporterTests
{
    [Fact]
    public void Exports_only_the_requested_columns_with_a_header()
    {
        var rows = new List<ListingRow>
        {
            new("1", "Sunny \"Loft\"", "New York", 210m, "Entire home", 3, 4.8, "40.7", "-74.0")
        };

        var csv = CsvExporter.Export(rows, ["name", "price"]);

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

        var csv = CsvExporter.Export(rows, ["name", "security_deposit"]);

        Assert.Equal("name,security_deposit\nSunny Loft,", csv);
    }
}
