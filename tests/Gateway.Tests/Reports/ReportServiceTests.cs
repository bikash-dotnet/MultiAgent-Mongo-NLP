using Gateway.Reports;
using Gateway.Rules;

namespace Gateway.Tests.Reports;

public class ReportServiceTests
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
    public void Builds_csv_for_confirmed_columns()
    {
        var build = ReportService.BuildCsv([Def("name"), Def("price")], awaitingApproval: false);

        Assert.True(build.Ready);
        Assert.StartsWith("name,price\n", build.Csv);
    }

    [Fact]
    public void Blocks_csv_while_awaiting_approval()
    {
        var build = ReportService.BuildCsv([Def("name")], awaitingApproval: true);

        Assert.False(build.Ready);
        Assert.Contains("awaiting approval", build.Reason);
    }

    [Fact]
    public void Uses_column_labels_as_headers()
    {
        var build = ReportService.BuildCsv([Def("name", "Guest Name")], awaitingApproval: false);

        Assert.True(build.Ready);
        Assert.StartsWith("Guest Name\n", build.Csv);
    }
}
