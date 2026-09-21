using Gateway.Reports;

namespace Gateway.Tests.Reports;

public class DemoListingSourceTests
{
    [Fact]
    public void Rows_are_deterministic_and_multi_market()
    {
        var first = DemoListingSource.Rows();
        var second = DemoListingSource.Rows();

        Assert.Equal(24, first.Count);
        Assert.True(first.Select(row => row.Market).Distinct().Count() >= 3);
        Assert.Equal(first[0].Name, second[0].Name);
        Assert.Equal(first[23].Price, second[23].Price);
    }
}
