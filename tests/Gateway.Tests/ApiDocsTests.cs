using System.Net;

namespace Gateway.Tests;

public class ApiDocsTests : IClassFixture<GatewayFactory>
{
    private readonly GatewayFactory _factory;

    public ApiDocsTests(GatewayFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OpenApi_document_lists_public_endpoints()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("/api/nlp/query", json);
        Assert.Contains("/api/session/greeting", json);
    }

    [Fact]
    public async Task Scalar_ui_is_served()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/scalar/v1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
