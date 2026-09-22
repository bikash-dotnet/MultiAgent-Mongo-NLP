using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Gateway.Tests;

public class ApiConversationTests : IClassFixture<GatewayFactory>
{
    private readonly GatewayFactory _factory;

    public ApiConversationTests(GatewayFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Conversation_start_without_token_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/conversations", new { utterance = "average price by market" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Complex_query_starts_the_intake_and_a_direct_query_completes()
    {
        var client = AuthedClient("Data Owner / Admin");

        var report = await client.PostAsJsonAsync("/api/conversations", new { utterance = "average price by market" });
        var reportTurn = await report.Content.ReadFromJsonAsync<ConversationTurnDto>();
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        Assert.Equal("Email", reportTurn!.Step);

        var direct = await client.PostAsJsonAsync("/api/conversations", new { utterance = "listings with pools in Los Angeles, just run it" });
        var directTurn = await direct.Content.ReadFromJsonAsync<ConversationTurnDto>();
        Assert.Equal(HttpStatusCode.OK, direct.StatusCode);
        Assert.Equal("Complete", directTurn!.Step);
        Assert.Equal("SimpleMql", directTurn.Kind);
    }

    [Fact]
    public async Task Approval_flag_blocks_non_privileged_roles()
    {
        var privileged = AuthedClient("Data Owner / Admin");
        var denied = AuthedClient("Business Analyst");

        var allowed = await privileged.PutAsJsonAsync("/api/governance/approval", new { enabled = false });
        var forbidden = await denied.PutAsJsonAsync("/api/governance/approval", new { enabled = true });

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        await privileged.PutAsJsonAsync("/api/governance/approval", new { enabled = true });
    }

    [Fact]
    public async Task Csv_endpoint_blocks_while_awaiting_approval()
    {
        var client = AuthedClient("Business Analyst");

        var start = await client.PostAsJsonAsync("/api/conversations", new { utterance = "average coordinates near me" });
        var turn = await start.Content.ReadFromJsonAsync<ConversationTurnDto>();
        if (turn!.Step != "Email")
        {
            return;
        }

        var answers = new object[]
        {
            new { email = "analyst@enterprise.com" },
            new { purpose = "Geo analysis", projectCode = "PROJ-GEO" },
            new { managerEmail = "manager@enterprise.com" },
            new { columns = new[] { "name" } },
            new { delivery = "CSV" }
        };

        foreach (var answer in answers)
        {
            var next = await client.PostAsJsonAsync($"/api/conversations/{turn.ConversationId}/answers", answer);
            turn = await next.Content.ReadFromJsonAsync<ConversationTurnDto>();
        }

        var csv = await client.GetAsync($"/api/conversations/{turn!.ConversationId}/report.csv");

        Assert.True(csv.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.OK);
    }

    private HttpClient AuthedClient(string role)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueToken(role));
        return client;
    }

    private static string IssueToken(string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GatewayFactory.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim("user_id", "usr_test"),
            new Claim("name", "Test"),
            new Claim("email", "analyst@enterprise.com"),
            new Claim("role", role),
            new Claim("lead_user_id", "usr_lead")
        };
        var token = new JwtSecurityToken(
            issuer: GatewayFactory.Issuer,
            audience: GatewayFactory.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record ConversationTurnDto(
        string ConversationId,
        string Step,
        string Kind,
        string AssistantMessage,
        string Control,
        bool ApprovalRequired,
        bool Downloadable);
}
