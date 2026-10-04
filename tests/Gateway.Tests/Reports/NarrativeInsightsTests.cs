using Gateway.Reports;

namespace Gateway.Tests.Reports;

public class NarrativeInsightsTests
{
    [Fact]
    public void Empty_input_reports_no_records()
    {
        var result = NarrativeInsights.Standard(["name"], []);

        Assert.Equal("No records matched the request.", result.Text);
        Assert.False(result.GeneratedByLlm);
    }

    [Fact]
    public void Report_counts_rows_and_computes_the_median()
    {
        var rows = new List<IReadOnlyDictionary<string, string?>>
        {
            new Dictionary<string, string?> { ["name"] = "a", ["price"] = "100" },
            new Dictionary<string, string?> { ["name"] = "b", ["price"] = "300" },
            new Dictionary<string, string?> { ["name"] = "c", ["price"] = "200" }
        };

        var result = NarrativeInsights.Standard(["name", "price"], rows);

        Assert.Contains("3 records", result.Text);
        Assert.Contains("median price is 200", result.Text);
        Assert.False(result.GeneratedByLlm);
    }

    [Fact]
    public void Report_names_the_top_market()
    {
        var rows = new List<IReadOnlyDictionary<string, string?>>
        {
            new Dictionary<string, string?> { ["address.market"] = "New York" },
            new Dictionary<string, string?> { ["address.market"] = "New York" },
            new Dictionary<string, string?> { ["address.market"] = "Sydney" }
        };

        var result = NarrativeInsights.Standard(["address.market"], rows);

        Assert.Contains("top market is New York with 2 records", result.Text);
    }

    [Fact]
    public void Report_is_deterministic_and_zero_token()
    {
        var rows = new List<IReadOnlyDictionary<string, string?>>
        {
            new Dictionary<string, string?> { ["name"] = "only" }
        };

        var first = NarrativeInsights.Standard(["name"], rows);
        var second = NarrativeInsights.Standard(["name"], rows);

        Assert.Equal(first.Text, second.Text);
        Assert.False(first.GeneratedByLlm);
    }
}
