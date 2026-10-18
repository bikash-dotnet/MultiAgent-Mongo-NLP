using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Gateway.Auth;
using Gateway.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Gateway.Tests;

public class ApiConfigTests : IClassFixture<GatewayFactory>, IAsyncLifetime
{
    private readonly GatewayFactory _factory;
    private HttpClient _client = null!;

    public ApiConfigTests(GatewayFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _client = Client(_factory, "usr_admin", "Data Owner / Admin", null);
        await _client.PostAsJsonAsync("/api/config/reset", new { confirm = true });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Config_requires_authentication()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/config");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Config_list_returns_sections_with_source_and_masked_secrets()
    {
        var client = _client;

        var response = await client.GetAsync("/api/config");
        response.EnsureSuccessStatusCode();

        var sections = await response.Content.ReadFromJsonAsync<List<ConfigSectionResponse>>();
        Assert.NotNull(sections);
        Assert.Contains(sections!, s => s.SectionName == "Execution");
        Assert.Contains(sections!, s => s.SectionName == "Nlp.Cache");

        var execution = sections!.First(s => s.SectionName == "Execution");
        var timeout = execution.Values.First(v => v.Key == "TimeoutMs");
        Assert.Equal(5000, IntValue(timeout.Value));
        Assert.Equal(SettingsSource.Default, timeout.Source);

        // No secret values are returned anywhere.
        var payload = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("DEV-ONLY-CHANGE-ME", payload);
        Assert.DoesNotContain("mongodb://localhost", payload);
        Assert.DoesNotContain("TBD", payload);
        Assert.DoesNotContain("no-reply@enterprise.com", payload);
    }

    [Fact]
    public async Task Config_schema_returns_editable_metadata()
    {
        var client = _client;

        var response = await client.GetFromJsonAsync<ConfigSchemaResponse>("/api/config/schema");

        Assert.NotNull(response);
        Assert.Contains(response.Sections, s => s.SectionName == "Execution");
        var execution = response.Sections.First(s => s.SectionName == "Execution");
        var timeout = execution.Settings.First(s => s.Key == "TimeoutMs");
        Assert.True(timeout.Editable);
        Assert.False(timeout.Secret);
        Assert.Equal(1000, timeout.Min);
        Assert.Equal(60000, timeout.Max);
    }

    [Fact]
    public async Task Config_get_section_returns_effective_values()
    {
        var client = _client;

        var section = await client.GetFromJsonAsync<ConfigSectionResponse>("/api/config/Execution");

        Assert.NotNull(section);
        Assert.Equal("Execution", section.SectionName);
        var timeout = section.Values.First(v => v.Key == "TimeoutMs");
        Assert.Equal(5000, IntValue(timeout.Value));
        Assert.Equal(SettingsSource.Default, timeout.Source);
    }

    [Fact]
    public async Task Non_admin_cannot_mutate_configuration()
    {
        var client = Client(_factory, "usr_analyst", "Business Analyst", null);

        var response = await client.PutAsJsonAsync("/api/config/Execution", new
        {
            values = new { TimeoutMs = 10000 },
            version = 0
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_update_execution_timeout_and_it_persists()
    {
        var client = _client;

        var put = await client.PutAsJsonAsync("/api/config/Execution", new
        {
            values = new { TimeoutMs = 15000 },
            version = 0
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var updated = await put.Content.ReadFromJsonAsync<ConfigSectionResponse>();
        var timeout = updated!.Values.First(v => v.Key == "TimeoutMs");
        Assert.Equal(15000, IntValue(timeout.Value));
        Assert.Equal(SettingsSource.Database, timeout.Source);

        // Read back reflects the override.
        var again = await client.GetFromJsonAsync<ConfigSectionResponse>("/api/config/Execution");
        Assert.Equal(15000, IntValue(again!.Values.First(v => v.Key == "TimeoutMs").Value));
        Assert.Equal(SettingsSource.Database, again.Values.First(v => v.Key == "TimeoutMs").Source);
    }

    [Fact]
    public async Task Stale_version_is_rejected_with_conflict()
    {
        var client = _client;

        await client.PutAsJsonAsync("/api/config/Execution", new
        {
            values = new { TimeoutMs = 15000 },
            version = 0
        });

        // version 0 is now stale (store is at version 1).
        var conflict = await client.PutAsJsonAsync("/api/config/Execution", new
        {
            values = new { TimeoutMs = 20000 },
            version = 0
        });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Out_of_range_timeout_is_rejected()
    {
        var client = _client;

        var response = await client.PutAsJsonAsync("/api/config/Execution", new
        {
            values = new { TimeoutMs = 999 },
            version = 0
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enterprise_core_rest_requires_base_url()
    {
        var client = _client;

        var response = await client.PutAsJsonAsync("/api/config/Execution", new
        {
            values = new { DataSource = "EnterpriseCoreREST" },
            version = 0
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Deleting_section_override_restores_default()
    {
        var client = _client;

        await client.PutAsJsonAsync("/api/config/Execution", new
        {
            values = new { TimeoutMs = 15000 },
            version = 0
        });

        var del = await client.DeleteAsync("/api/config/Execution");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var section = await client.GetFromJsonAsync<ConfigSectionResponse>("/api/config/Execution");
        Assert.NotNull(section);
        var timeout = section!.Values.First(v => v.Key == "TimeoutMs");
        Assert.Equal(5000, IntValue(timeout.Value));
        Assert.Equal(SettingsSource.Default, timeout.Source);
    }

    [Fact]
    public async Task Reset_requires_confirm()
    {
        var client = _client;

        var response = await client.PostAsJsonAsync("/api/config/reset", new { confirm = false });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reset_clears_all_overrides()
    {
        var client = _client;

        await client.PutAsJsonAsync("/api/config/Execution", new
        {
            values = new { TimeoutMs = 15000 },
            version = 0
        });

        var reset = await client.PostAsJsonAsync("/api/config/reset", new { confirm = true });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        var section = await client.GetFromJsonAsync<ConfigSectionResponse>("/api/config/Execution");
        Assert.NotNull(section);
        Assert.Equal(5000, IntValue(section!.Values.First(v => v.Key == "TimeoutMs").Value));
        Assert.Equal(SettingsSource.Default, section.Values.First(v => v.Key == "TimeoutMs").Source);
    }

    private static HttpClient Client(GatewayFactory factory, string userId, string role, string? leadUserId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueToken(userId, role, leadUserId));
        return client;
    }

    private static string IssueToken(string userId, string role, string? leadUserId)
    {
        var claims = new List<Claim>
        {
            new(SessionClaims.UserId, userId),
            new(ClaimTypes.Name, userId),
            new(SessionClaims.Role, role)
        };
        if (leadUserId is not null)
        {
            claims.Add(new(SessionClaims.LeadUserId, leadUserId));
        }

        var key = GatewayFactory.SigningKey;
        var token = new JwtSecurityToken(
            issuer: GatewayFactory.Issuer,
            audience: GatewayFactory.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256)
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static int IntValue(object value) => ((JsonElement)value).GetInt32();
}
