namespace Gateway.Reports;

public sealed record ReportBuild(bool Ready, string? Csv, string? Reason);

public static class ReportService
{
    public static ReportBuild BuildCsv(IReadOnlyList<string> columns, bool awaitingApproval)
    {
        if (awaitingApproval)
        {
            return new ReportBuild(
                false,
                null,
                "This report is awaiting approval and cannot be downloaded yet.");
        }

        if (columns.Count == 0)
        {
            return new ReportBuild(false, null, "No columns were confirmed for this report.");
        }

        return new ReportBuild(true, CsvExporter.Export(DemoListingSource.Rows(), columns), null);
    }
}
