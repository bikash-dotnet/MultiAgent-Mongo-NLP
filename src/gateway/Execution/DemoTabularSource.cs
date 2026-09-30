using System.Globalization;
using Gateway.Reports;

namespace Gateway.Execution;

public static class DemoTabularSource
{
    public static TabularResult Build(ExecutionRequest request)
    {
        var rows = DemoListingSource.Rows()
            .Select(row => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["id"] = row.Id,
                ["name"] = row.Name,
                ["address.market"] = row.Market,
                ["price"] = row.Price.ToString(CultureInfo.InvariantCulture),
                ["room_type"] = row.RoomType,
                ["accommodates"] = row.Accommodates.ToString(CultureInfo.InvariantCulture),
                ["review_scores.rating"] = row.Rating.ToString("0.0", CultureInfo.InvariantCulture),
                ["address.location.coordinates"] = row.Latitude is null ? null : $"{row.Latitude},{row.Longitude}"
            })
            .ToList();

        var columns = request.Columns.Count > 0 ? request.Columns : ["name", "address.market", "price"];
        return new TabularResult(columns, rows, request.DataSource.ToString(), 0);
    }
}
