using Gateway.Reports;

namespace Gateway.Tests.Reports;

public class ReportServiceTests
{
    [Fact]
    public void Builds_csv_for_confirmed_columns()
    {
        var build = ReportService.BuildCsv(["name", "price"], awaitingApproval: false);

        Assert.True(build.Ready);
        Assert.StartsWith("name,price\n", build.Csv);
    }

    [Fact]
    public void Blocks_csv_while_awaiting_approval()
    {
        var build = ReportService.BuildCsv(["name"], awaitingApproval: true);

        Assert.False(build.Ready);
        Assert.Contains("awaiting approval", build.Reason);
    }
}
