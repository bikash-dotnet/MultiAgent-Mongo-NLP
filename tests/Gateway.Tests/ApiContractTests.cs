using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Gateway.Tests;

public class ApiContractTests : IClassFixture<GatewayFactory>
{
    private readonly GatewayFactory _factory;

    public ApiContractTests(GatewayFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Development_token_is_anonymous()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/dev/token");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.False(string.IsNullOrWhiteSpace(payload?.Token));
    }

    [Fact]
    public async Task Health_is_anonymous()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("ok", payload?.Status);
    }

    [Fact]
    public async Task Greeting_without_token_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/session/greeting");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Agent_stream_without_token_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/agents/stream");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Greeting_returns_time_aware_chips_for_authenticated_user()
    {
        var client = AuthedClient();

        var response = await client.GetAsync("/api/session/greeting");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<GreetingDto>();
        Assert.NotNull(payload);
        Assert.Equal("Bikash", payload!.DisplayName);
        Assert.Equal("morning", payload.Period);
        Assert.Equal("Hi Bikash, Good morning", payload.Message);
        Assert.NotEmpty(payload.Chips);
    }

    [Fact]
    public async Task Agent_stream_emits_idle_heartbeat()
    {
        var client = AuthedClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/agents/stream");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream);
        var first = await reader.ReadLineAsync(cts.Token);
        var second = await reader.ReadLineAsync(cts.Token);
        Assert.Equal("event: agent.idle", first);
        Assert.StartsWith("data:", second);
        Assert.Contains("idle", second);
    }

    [Fact]
    public async Task Authenticated_request_echoes_session_correlation_header()
    {
        var client = AuthedClient();
        client.DefaultRequestHeaders.Add("X-Session-Id", "sess_9041-A");

        var response = await client.GetAsync("/api/session/greeting");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("sess_9041-A", response.Headers.GetValues("X-Session-Id").Single());
    }

    private HttpClient AuthedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueToken());
        return client;
    }

    private static string IssueToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GatewayFactory.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim("user_id", "usr_bn_101"),
            new Claim("name", "Bikash"),
            new Claim("email", "bnayak@enterprise.com"),
            new Claim("role", "Data Owner / Admin"),
            new Claim("lead_user_id", "usr_bn_101")
        };
        var token = new JwtSecurityToken(
            issuer: GatewayFactory.Issuer,
            audience: GatewayFactory.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record TokenResponse(string Token);

    private sealed record HealthResponse(string Status);

    private sealed record GreetingDto(string DisplayName, string Period, string Message, string[] Chips);
}
