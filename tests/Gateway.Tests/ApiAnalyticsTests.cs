using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Gateway.Tests;

public class ApiAnalyticsTests : IClassFixture<GatewayFactory>
{
    private readonly GatewayFactory _factory;

    public ApiAnalyticsTests(GatewayFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Analytics_requires_authentication()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/admin/analytics");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Analytics_returns_aggregated_metrics()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueToken());

        var response = await client.GetAsync("/api/admin/analytics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<AnalyticsDto>();
        Assert.NotNull(payload);
        Assert.True(payload!.TotalExecutions >= 0);
        Assert.NotNull(payload.ExportCounts);
        Assert.NotNull(payload.DataSourceCounts);
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

    private sealed record AnalyticsDto(
        int TotalExecutions,
        int SemanticCacheHits,
        double SemanticCacheHitRate,
        int TotalLlmTokens,
        Dictionary<string, int> ExportCounts,
        Dictionary<string, int> DataSourceCounts);
}
