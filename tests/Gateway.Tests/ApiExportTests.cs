using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Gateway.Tests;

public class ApiExportTests : IClassFixture<GatewayFactory>
{
    private readonly GatewayFactory _factory;

    public ApiExportTests(GatewayFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Xlsx_download_returns_spreadsheet_for_completed_intake()
    {
        var client = AuthedClient();
        var id = await CompleteIntakeAsync(client);

        var response = await client.GetAsync($"/api/conversations/{id}/report.xlsx");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("application/vnd", response.Content.Headers.ContentType!.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);
    }

    [Fact]
    public async Task Briefing_returns_a_deterministic_summary()
    {
        var client = AuthedClient();
        var id = await CompleteIntakeAsync(client);

        var response = await client.PostAsync($"/api/conversations/{id}/report/briefing", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<BriefingDto>();
        Assert.NotNull(payload);
        Assert.False(payload!.GeneratedByLlm);
        Assert.False(string.IsNullOrWhiteSpace(payload.Text));
    }

    [Fact]
    public async Task Email_dispatches_a_pdf_attachment()
    {
        var client = AuthedClient();
        var id = await CompleteIntakeAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/conversations/{id}/report/email",
            new { recipient = "lead@enterprise.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var message = Assert.Single(_factory.Emails.Sent);
        Assert.Equal("lead@enterprise.com", message.Recipient);
        Assert.NotNull(message.Attachment);
        Assert.Equal("application/pdf", message.Attachment!.ContentType);
    }

    [Fact]
    public async Task Export_endpoints_require_authentication()
    {
        var client = _factory.CreateClient();

        var xlsx = await client.GetAsync("/api/conversations/none/report.xlsx");
        var briefing = await client.PostAsync("/api/conversations/none/report/briefing", null);

        Assert.Equal(HttpStatusCode.Unauthorized, xlsx.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, briefing.StatusCode);
    }

    private async Task<string> CompleteIntakeAsync(HttpClient client)
    {
        var start = await client.PostAsJsonAsync("/api/conversations", new { utterance = "average price by market" });
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var turn = await start.Content.ReadFromJsonAsync<ConversationTurnDto>();

        turn = await AnswerAsync(client, turn!.ConversationId, new { email = "analyst@enterprise.com" });
        turn = await AnswerAsync(client, turn.ConversationId, new { purpose = "Geo analysis", projectCode = "PROJ-GEO" });
        turn = await AnswerAsync(client, turn.ConversationId, new { managerEmail = "manager@enterprise.com" });
        turn = await AnswerAsync(client, turn.ConversationId, new { columns = new[] { "name" } });
        turn = await AnswerAsync(client, turn.ConversationId, new { delivery = "CSV" });

        Assert.Equal("Complete", turn.Step);
        return turn.ConversationId;
    }

    private static async Task<ConversationTurnDto> AnswerAsync(HttpClient client, string conversationId, object answer)
    {
        var next = await client.PostAsJsonAsync($"/api/conversations/{conversationId}/answers", answer);
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        return (await next.Content.ReadFromJsonAsync<ConversationTurnDto>())!;
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
            new Claim("role", "Business Analyst"),
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

    private sealed record ConversationTurnDto(string ConversationId, string Step, string? EmailPrefill);

    private sealed record BriefingDto(string Text, bool GeneratedByLlm);
}
