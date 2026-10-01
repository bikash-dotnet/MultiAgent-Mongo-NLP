using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Gateway.Tests;

public class ApiExecutionTests : IClassFixture<GatewayFactory>
{
    private readonly GatewayFactory _factory;

    public ApiExecutionTests(GatewayFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Simple_query_returns_executed_rows()
    {
        var client = AuthedClient();

        var response = await client.PostAsJsonAsync("/api/nlp/query", new { utterance = "listings with pools in Los Angeles, just run it" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<NlpExecutionDto>();
        Assert.NotNull(payload);
        Assert.Equal("Stub", payload!.DataSource);
        Assert.Equal(1, payload.RowCount);
        Assert.NotNull(payload.Rows);
        Assert.NotEmpty(payload.Rows!);
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

    private sealed record NlpExecutionDto(string Kind, string? DataSource, int? RowCount, List<Dictionary<string, string?>>? Rows);
}
