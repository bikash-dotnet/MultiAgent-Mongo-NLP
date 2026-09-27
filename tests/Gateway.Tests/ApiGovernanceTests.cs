using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Gateway.Conversations;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Router;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Gateway.Tests;

public class ApiGovernanceTests : IClassFixture<GatewayFactory>
{
    private readonly GatewayFactory _factory;

    public ApiGovernanceTests(GatewayFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Queue_requires_a_token()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/access-requests");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Analyst_queue_contains_only_own_requests()
    {
        var request = await SeedPendingAsync("usr_analyst", "usr_lead");
        var client = Client("usr_analyst", "Business Analyst", "usr_lead");

        var results = await client.GetFromJsonAsync<List<AccessRequestDto>>("/api/access-requests");

        Assert.NotNull(results);
        Assert.Contains(results!, item => item.Id == request.Id);
        Assert.All(results!, item => Assert.Equal("usr_analyst", item.Requester!.UserId));
    }

    [Fact]
    public async Task Team_lead_approves_an_assigned_request_and_resumes_the_report()
    {
        var request = await SeedPendingAsync("usr_analyst", "usr_lead");
        var conversation = await SeedPausedConversationAsync(request.Id);
        var client = Client("usr_lead", "Team Lead", "usr_director");

        var response = await client.PostAsJsonAsync($"/api/access-requests/{request.Id}/approve", new { notes = "approved" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<AccessRequestDto>();
        Assert.Equal("APPROVED", updated!.Status);
        Assert.False(updated.Resolution!.OverrideInvoked);

        var turn = await client.GetFromJsonAsync<ConversationTurnDto>($"/api/conversations/{conversation}");
        Assert.False(turn!.ApprovalRequired);
        Assert.True(turn.Downloadable);
    }

    [Fact]
    public async Task Analyst_cannot_approve()
    {
        var request = await SeedPendingAsync("usr_analyst", "usr_lead");
        var client = Client("usr_analyst", "Business Analyst", "usr_lead");

        var response = await client.PostAsJsonAsync($"/api/access-requests/{request.Id}/approve", new { notes = "x" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Manager_override_records_tags_and_requires_notes()
    {
        var request = await SeedPendingAsync("usr_analyst", "usr_lead");
        var client = Client("usr_mgr", "Engineering Manager", null);

        var blank = await client.PostAsJsonAsync($"/api/access-requests/{request.Id}/override", new { notes = "" });
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);

        var response = await client.PostAsJsonAsync($"/api/access-requests/{request.Id}/override", new { notes = "urgent unblock" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<AccessRequestDto>();
        Assert.True(updated!.Resolution!.OverrideInvoked);
        Assert.Equal("HIERARCHICAL_MANAGEMENT_OVERRIDE", updated.Resolution.OverrideType);
    }

    [Fact]
    public async Task Resolving_twice_conflicts()
    {
        var request = await SeedPendingAsync("usr_analyst", "usr_lead");
        var client = Client("usr_lead", "Team Lead", "usr_director");
        await client.PostAsJsonAsync($"/api/access-requests/{request.Id}/approve", new { notes = "ok" });

        var second = await client.PostAsJsonAsync($"/api/access-requests/{request.Id}/reject", new { notes = "no" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Missing_request_is_not_found()
    {
        var client = Client("usr_lead", "Team Lead", "usr_director");

        var response = await client.PostAsJsonAsync("/api/access-requests/missing/approve", new { notes = "x" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<AccessRequest> SeedPendingAsync(string requester, string lead)
    {
        var store = _factory.Services.GetRequiredService<IAccessRequestStore>();
        return await store.CreateAsync(new AccessRequest(
            Guid.NewGuid().ToString("N"),
            "sess_seed",
            "[]",
            ["address.location.coordinates"],
            AccessRequest.PendingLead,
            null,
            DateTimeOffset.UnixEpoch,
            Requester: new RequesterInfo(requester, "Requester", "Business Analyst", lead),
            AssignedLeadId: lead,
            RequestedFlags: [new RequestedFlag("address.location.coordinates", "requires_approval")]));
    }

    private async Task<string> SeedPausedConversationAsync(string accessRequestId)
    {
        var store = _factory.Services.GetRequiredService<IConversationStore>();
        var state = new ConversationState(
            Guid.NewGuid().ToString("N"),
            "sess_seed",
            ConversationStep.Complete,
            "average coordinates near me",
            "[]",
            NlpRouteKind.GovernancePaused,
            accessRequestId,
            ["address.location.coordinates"],
            false,
            true,
            new ReportIntakeDraft(
                RequesterEmail: "analyst@enterprise.com",
                Purpose: "Geo analysis",
                BusinessImpact: "Target market expansion planning",
                ProjectCode: "PROJ-1",
                ManagerEmail: "manager@enterprise.com",
                Columns: ["name"],
                DeliveryFormat: "CSV"),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);
        await store.CreateAsync(state);
        return state.Id;
    }

    private HttpClient Client(string userId, string role, string? leadUserId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueToken(userId, role, leadUserId));
        return client;
    }

    private static string IssueToken(string userId, string role, string? leadUserId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GatewayFactory.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new("user_id", userId),
            new("name", userId),
            new("email", $"{userId}@enterprise.com"),
            new("role", role)
        };
        if (leadUserId is not null)
        {
            claims.Add(new Claim("lead_user_id", leadUserId));
        }

        var token = new JwtSecurityToken(
            issuer: GatewayFactory.Issuer,
            audience: GatewayFactory.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record AccessRequestDto(
        string Id,
        string Status,
        RequesterDto? Requester,
        ResolutionDto? Resolution);

    private sealed record RequesterDto(string UserId, string Name, string Role, string? LeadUserId);

    private sealed record ResolutionDto(
        string AssignedLeadId,
        string ResolvedByUserId,
        bool OverrideInvoked,
        string? OverrideType,
        string? Notes);

    private sealed record ConversationTurnDto(bool ApprovalRequired, bool Downloadable);
}
