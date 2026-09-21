namespace Gateway.Reports;

public static class DemoListingSource
{
    private static readonly string[] Markets = ["New York", "Los Angeles", "Sydney"];

    public static IReadOnlyList<ListingRow> Rows()
    {
        var rows = new List<ListingRow>(24);

        for (var index = 0; index < 24; index++)
        {
            var market = Markets[index % Markets.Length];
            rows.Add(new ListingRow(
                $"lst_{index + 1:D3}",
                $"Demo Listing {index + 1} in {market}",
                market,
                90m + (index * 15m),
                index % 2 == 0 ? "Entire home/apt" : "Private room",
                2 + (index % 5),
                4.0 + ((index % 10) / 10.0),
                (40.70 + (index * 0.01)).ToString("0.00"),
                (-74.00 - (index * 0.01)).ToString("0.00")));
        }

        return rows;
    }
}
