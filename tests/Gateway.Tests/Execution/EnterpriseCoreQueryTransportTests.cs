using System.Net;
using System.Text;
using Gateway.Execution;
using Gateway.Governance;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Execution;

public class EnterpriseCoreQueryTransportTests
{
    [Fact]
    public async Task Maps_the_upstream_tabular_contract()
    {
        var json = """{"columns":["name","price"],"rows":[{"name":"a","price":"10"}]}""";
        var http = new HttpClient(new StubHandler(json))
        {
            BaseAddress = new Uri("http://enterprise.test")
        };
        var transport = new EnterpriseCoreQueryTransport(
            http,
            Microsoft.Extensions.Options.Options.Create(new EnterpriseCoreOptions { QueryPath = "/api/query" }));

        var result = await transport.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal("EnterpriseCoreREST", result.DataSource);
        Assert.Equal(["name", "price"], result.Columns);
        Assert.Equal("a", result.Rows[0]["name"]);
    }

    [Fact]
    public async Task Upstream_failure_produces_an_error_result()
    {
        var http = new HttpClient(new StubHandler("nope", HttpStatusCode.InternalServerError))
        {
            BaseAddress = new Uri("http://enterprise.test")
        };
        var transport = new EnterpriseCoreQueryTransport(
            http,
            Microsoft.Extensions.Options.Options.Create(new EnterpriseCoreOptions()));

        var result = await transport.ExecuteAsync(Request(), CancellationToken.None);

        Assert.NotNull(result.Error);
        Assert.Equal(0, result.RowCount);
    }

    private static ExecutionRequest Request()
    {
        return new ExecutionRequest(
            "[{\"$match\":{}}]",
            "sess-1",
            "Listings",
            ExecutionDataSource.EnterpriseCoreREST,
            "listingsAndReviews",
            ["name", "price"],
            new Dictionary<string, string>(),
            false,
            false,
            0,
            new RequesterContext("usr_1", "Bikash", "Business Analyst", null, null),
            GovernanceDecision.None);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly HttpStatusCode _status;

        public StubHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            _body = body;
            _status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
        }
    }
}
