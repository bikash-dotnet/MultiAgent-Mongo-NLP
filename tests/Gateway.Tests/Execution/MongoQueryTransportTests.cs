using Gateway.Execution;
using Gateway.Governance;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Execution;

public class MongoQueryTransportTests
{
    [Fact]
    public async Task Executes_against_mongo_when_a_test_connection_is_configured()
    {
        var connection = Environment.GetEnvironmentVariable("MONGODB_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
        {
            return;
        }

        var options = Microsoft.Extensions.Options.Options.Create(new ExecutionOptions
        {
            Database = Environment.GetEnvironmentVariable("MONGODB_TEST_DATABASE") ?? "sample_airbnb",
            Collection = "listingsAndReviews"
        });
        var transport = new MongoQueryTransport(connection, options);
        var request = new ExecutionRequest(
            "[{\"$match\":{\"address.market\":\"New York\"}},{\"$limit\":3}]",
            "sess-1",
            "Listings in New York",
            ExecutionDataSource.Mongo,
            "listingsAndReviews",
            ["name", "address.market"],
            new Dictionary<string, string>(),
            false,
            false,
            0,
            new RequesterContext("usr_1", "Bikash", "Data Owner / Admin", null, null),
            GovernanceDecision.None);

        var result = await transport.ExecuteAsync(request, CancellationToken.None);

        Assert.Null(result.Error);
        Assert.True(result.RowCount <= 3);
    }

    [Fact]
    public async Task Invalid_pipeline_json_produces_an_error_result()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new ExecutionOptions());
        var transport = new MongoQueryTransport("mongodb://localhost:27017", options);
        var request = new ExecutionRequest(
            "not-json",
            "sess-1",
            "bad",
            ExecutionDataSource.Mongo,
            "listingsAndReviews",
            ["name"],
            new Dictionary<string, string>(),
            false,
            false,
            0,
            new RequesterContext("usr_1", "Bikash", "Business Analyst", null, null),
            GovernanceDecision.None);

        await Assert.ThrowsAsync<MongoDB.Bson.BsonException>(() => transport.ExecuteAsync(request));
    }
}
