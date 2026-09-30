using Gateway.Execution;
using Gateway.Governance;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Execution;

public class RoutingQueryExecutorTests
{
    [Fact]
    public async Task Routes_to_mongo_by_default()
    {
        var mongo = new NamedTransport("mongo");
        var rest = new NamedTransport("rest");
        var router = new RoutingQueryExecutor(mongo, rest, Ops());

        var result = await router.ExecuteAsync(Request(ExecutionDataSource.Mongo), CancellationToken.None);

        Assert.Equal("mongo", result.DataSource);
    }

    [Fact]
    public async Task Routes_to_rest_when_policy_selects_it()
    {
        var mongo = new NamedTransport("mongo");
        var rest = new NamedTransport("rest");
        var router = new RoutingQueryExecutor(mongo, rest, Ops());

        var result = await router.ExecuteAsync(Request(ExecutionDataSource.EnterpriseCoreREST), CancellationToken.None);

        Assert.Equal("rest", result.DataSource);
    }

    [Fact]
    public void Demo_source_maps_requested_columns()
    {
        var result = DemoTabularSource.Build(Request(ExecutionDataSource.Mongo));

        Assert.Equal(24, result.RowCount);
        Assert.Equal(["name", "address.market", "price"], result.Columns);
        Assert.NotNull(result.Rows[0]["name"]);
    }

    private static IOptions<ExecutionOptions> Ops()
    {
        return Microsoft.Extensions.Options.Options.Create(new ExecutionOptions());
    }

    private static ExecutionRequest Request(ExecutionDataSource source)
    {
        return new ExecutionRequest(
            "[{\"$match\":{}}]",
            "sess-1",
            "Listings",
            source,
            "listingsAndReviews",
            ["name", "address.market", "price"],
            new Dictionary<string, string>(),
            false,
            false,
            0,
            new RequesterContext("usr_1", "Bikash", "Business Analyst", null, null),
            GovernanceDecision.None);
    }

    private sealed class NamedTransport : IQueryTransport
    {
        private readonly string _name;

        public NamedTransport(string name)
        {
            _name = name;
        }

        public Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(TabularResult.Empty(_name));
        }
    }
}
