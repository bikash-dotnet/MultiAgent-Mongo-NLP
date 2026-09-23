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
    public async Task Csv_download_returns_ready_report_for_completed_demo_intake()
    {
        var client = AuthedClient("Business Analyst");

        var start = await client.PostAsJsonAsync("/api/conversations", new { utterance = "average price by market" });
        var turn = await start.Content.ReadFromJsonAsync<ConversationTurnDto>();
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        Assert.Equal("Email", turn!.Step);

        turn = await AnswerAsync(client, turn.ConversationId, new { email = "analyst@enterprise.com" });
        Assert.Equal("Purpose", turn.Step);
        turn = await AnswerAsync(client, turn.ConversationId, new { purpose = "Geo analysis", projectCode = "PROJ-GEO" });
        Assert.Equal("ManagerEmail", turn.Step);
        turn = await AnswerAsync(client, turn.ConversationId, new { managerEmail = "manager@enterprise.com" });
        Assert.Equal("Columns", turn.Step);
        turn = await AnswerAsync(client, turn.ConversationId, new { columns = new[] { "name" } });
        Assert.Equal("Delivery", turn.Step);
        turn = await AnswerAsync(client, turn.ConversationId, new { delivery = "CSV" });

        Assert.Equal("Complete", turn.Step);
        Assert.False(turn.ApprovalRequired);
        Assert.True(turn.Downloadable);

        var csv = await client.GetAsync($"/api/conversations/{turn.ConversationId}/report.csv");

        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.StartsWith("text/csv", csv.Content.Headers.ContentType!.ToString());

        var body = await csv.Content.ReadAsStringAsync();
        var lines = body.Split('\n');
        Assert.StartsWith("name", body);
        Assert.True(lines.Length >= 2, "expected the CSV to contain at least one data line");
        Assert.False(string.IsNullOrWhiteSpace(lines[1]));
    }

    [Fact]
    public async Task Csv_download_returns_conflict_before_intake_completes()
    {
        var client = AuthedClient("Business Analyst");

        var start = await client.PostAsJsonAsync("/api/conversations", new { utterance = "average price by market" });
        var turn = await start.Content.ReadFromJsonAsync<ConversationTurnDto>();
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        Assert.Equal("Email", turn!.Step);

        var csv = await client.GetAsync($"/api/conversations/{turn.ConversationId}/report.csv");

        Assert.Equal(HttpStatusCode.Conflict, csv.StatusCode);
    }

    [Fact]
    public async Task Unknown_conversation_returns_not_found()
    {
        var client = AuthedClient("Business Analyst");
        const string unknownId = "does-not-exist";

        var get = await client.GetAsync($"/api/conversations/{unknownId}");
        var answers = await client.PostAsJsonAsync($"/api/conversations/{unknownId}/answers", new { email = "analyst@enterprise.com" });
        var csv = await client.GetAsync($"/api/conversations/{unknownId}/report.csv");

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, answers.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, csv.StatusCode);
    }

    [Fact]
    public async Task Blank_utterance_returns_bad_request()
    {
        var client = AuthedClient("Business Analyst");

        var response = await client.PostAsJsonAsync("/api/conversations", new { utterance = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Governance_approval_endpoint_reports_the_flag()
    {
        var client = AuthedClient("Business Analyst");

        var response = await client.GetAsync("/api/governance/approval");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApprovalDto>();
        Assert.NotNull(payload);
    }

    private async Task<ConversationTurnDto> AnswerAsync(HttpClient client, string conversationId, object answer)
    {
        var next = await client.PostAsJsonAsync($"/api/conversations/{conversationId}/answers", answer);
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        var turn = await next.Content.ReadFromJsonAsync<ConversationTurnDto>();
        Assert.NotNull(turn);
        return turn!;
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

    private sealed record ApprovalDto(bool Enabled);
}
