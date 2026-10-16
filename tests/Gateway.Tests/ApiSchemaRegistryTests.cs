using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Gateway.Schemas;
using Microsoft.IdentityModel.Tokens;

namespace Gateway.Tests;

public class ApiSchemaRegistryTests : IClassFixture<GatewayFactory>
{
    private readonly GatewayFactory _factory;

    public ApiSchemaRegistryTests(GatewayFactory factory)
    {
        _factory = factory;
    }

    private const string SeededSchemaId = "sch_airbnb_listings";

    [Fact]
    public async Task List_schemas_requires_a_token()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/schemas");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_schemas_returns_seeded_schema()
    {
        var client = Client("usr_analyst", "Business Analyst", null);

        var response = await client.GetFromJsonAsync<SchemaListDto>("/api/schemas");

        Assert.NotNull(response);
        Assert.Contains(response!.Schemas, s => s.Id == SeededSchemaId);
        Assert.Contains(response!.Schemas, s => s.IsActiveSource);
    }

    [Fact]
    public async Task Get_schema_by_id_returns_detail()
    {
        var client = Client("usr_analyst", "Business Analyst", null);

        var response = await client.GetFromJsonAsync<SchemaDetailDto>($"/api/schemas/{SeededSchemaId}");

        Assert.NotNull(response);
        Assert.Equal(SeededSchemaId, response!.Schema.Id);
        Assert.True(response.Schema.Fields.Count > 0);
    }

    [Fact]
    public async Task Get_missing_schema_returns_404()
    {
        var client = Client("usr_analyst", "Business Analyst", null);

        var response = await client.GetAsync("/api/schemas/does_not_exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_schema_requires_data_owner_or_admin()
    {
        var client = Client("usr_analyst", "Business Analyst", null);

        var response = await client.PostAsJsonAsync("/api/schemas", new
        {
            name = "Test Schema",
            dataSource = "Mongo",
            database = "sample_airbnb",
            collection = "listingsAndReviews",
            fields = new[]
            {
                new { path = "price", dataType = "Number", isSensitive = false, requiresApproval = false, dataOwnerRoles = Array.Empty<string>(), description = "" }
            }
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_schema_rejects_empty_name()
    {
        var client = Client("usr_owner", "Data Owner / Admin", null);

        var response = await client.PostAsJsonAsync("/api/schemas", new
        {
            name = "",
            dataSource = "Mongo",
            database = "sample_airbnb",
            collection = "listingsAndReviews",
            fields = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_schema_rejects_duplicate_field_paths()
    {
        var client = Client("usr_owner", "Data Owner / Admin", null);

        var response = await client.PostAsJsonAsync("/api/schemas", new
        {
            name = "Dup Schema",
            dataSource = "Mongo",
            database = "sample_airbnb",
            collection = "listingsAndReviews",
            fields = new[]
            {
                new { path = "price", dataType = "Number", isSensitive = false, requiresApproval = false, dataOwnerRoles = Array.Empty<string>(), description = "" },
                new { path = "price", dataType = "Number", isSensitive = false, requiresApproval = false, dataOwnerRoles = Array.Empty<string>(), description = "" }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_schema_succeeds_for_data_owner()
    {
        var client = Client("usr_owner", "Data Owner / Admin", null);

        var response = await client.PostAsJsonAsync("/api/schemas", new
        {
            name = "New Schema",
            dataSource = "Mongo",
            database = "sample_airbnb",
            collection = "listingsAndReviews",
            fields = new[]
            {
                new { path = "price", dataType = "Number", isSensitive = false, requiresApproval = false, dataOwnerRoles = Array.Empty<string>(), description = "" },
                new { path = "address.market", dataType = "String", isSensitive = false, requiresApproval = false, dataOwnerRoles = Array.Empty<string>(), description = "" }
            }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<SchemaDetailDto>();
        Assert.NotNull(created);
        Assert.Equal("New Schema", created!.Schema.Name);
        Assert.Equal(2, created.Schema.Fields.Count);
        Assert.Equal(1, created.Schema.Version);
        Assert.False(created.Schema.IsActiveSource);

        // Clean up the created schema is not supported via API; leave it.
    }

    [Fact]
    public async Task Update_schema_requires_data_owner_or_admin()
    {
        var client = Client("usr_analyst", "Business Analyst", null);

        var response = await client.PutAsJsonAsync($"/api/schemas/{SeededSchemaId}", new
        {
            name = "Renamed",
            dataSource = "Mongo",
            database = "sample_airbnb",
            collection = "listingsAndReviews",
            fields = Array.Empty<object>(),
            version = 1,
            isActiveSource = true
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_schema_with_stale_version_returns_409()
    {
        var client = Client("usr_owner", "Data Owner / Admin", null);

        var response = await client.PutAsJsonAsync($"/api/schemas/{SeededSchemaId}", new
        {
            name = "Renamed",
            dataSource = "Mongo",
            database = "sample_airbnb",
            collection = "listingsAndReviews",
            fields = Array.Empty<object>(),
            version = 999,
            isActiveSource = true
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_schema_succeeds_with_correct_version()
    {
        var client = Client("usr_owner", "Data Owner / Admin", null);

        // First fetch to get current version
        var current = await client.GetFromJsonAsync<SchemaDetailDto>($"/api/schemas/{SeededSchemaId}");
        var version = current!.Schema.Version;

        var response = await client.PutAsJsonAsync($"/api/schemas/{SeededSchemaId}", new
        {
            name = "Renamed Airbnb Listings",
            dataSource = "Mongo",
            database = "sample_airbnb",
            collection = "listingsAndReviews",
            fields = current.Schema.Fields.Select(f => new
            {
                path = f.Path,
                dataType = f.DataType,
                isSensitive = f.IsSensitive,
                requiresApproval = f.RequiresApproval,
                dataOwnerRoles = f.DataOwnerRoles,
                description = f.Description
            }).ToList(),
            version,
            isActiveSource = true
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<SchemaDetailDto>();
        Assert.NotNull(updated);
        Assert.Equal("Renamed Airbnb Listings", updated!.Schema.Name);
        Assert.True(updated.Schema.IsActiveSource);
        Assert.True(updated.Schema.Version > version);
    }

    [Fact]
    public async Task Add_field_to_schema()
    {
        var client = Client("usr_owner", "Data Owner / Admin", null);

        var response = await client.PostAsJsonAsync($"/api/schemas/{SeededSchemaId}/fields", new
        {
            path = "temp_test_field",
            dataType = "Number",
            isSensitive = false,
            requiresApproval = false,
            dataOwnerRoles = Array.Empty<string>(),
            description = "Average review score"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<SchemaDetailDto>();
        Assert.NotNull(updated);
        Assert.Contains(updated!.Schema.Fields, f => f.Path == "temp_test_field" && f.Description == "Average review score");
    }

    [Fact]
    public async Task Update_field_on_schema()
    {
        var client = Client("usr_owner", "Data Owner / Admin", null);

        var response = await client.PutAsJsonAsync($"/api/schemas/{SeededSchemaId}/fields/price", new
        {
            path = "price",
            dataType = "Number",
            isSensitive = true,
            requiresApproval = true,
            dataOwnerRoles = new[] { "Data Owner / Admin" },
            description = "Listing price"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<SchemaDetailDto>();
        Assert.NotNull(updated);
        var priceField = updated!.Schema.Fields.FirstOrDefault(f => f.Path == "price");
        Assert.NotNull(priceField);
        Assert.True(priceField.IsSensitive);
        Assert.True(priceField.RequiresApproval);
        Assert.Equal("Listing price", priceField.Description);
    }

    [Fact]
    public async Task Delete_field_from_schema()
    {
        var client = Client("usr_owner", "Data Owner / Admin", null);

        // First add a field
        await client.PostAsJsonAsync($"/api/schemas/{SeededSchemaId}/fields", new
        {
            path = "temp_field_to_delete",
            dataType = "String",
            isSensitive = false,
            requiresApproval = false,
            dataOwnerRoles = Array.Empty<string>(),
            description = ""
        });

        var response = await client.DeleteAsync($"/api/schemas/{SeededSchemaId}/fields/temp_field_to_delete");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<SchemaDetailDto>();
        Assert.NotNull(updated);
        Assert.DoesNotContain(updated!.Schema.Fields, f => f.Path == "temp_field_to_delete");
    }

    [Fact]
    public async Task Activate_schema()
    {
        var client = Client("usr_owner", "Data Owner / Admin", null);

        // Create a new schema first (inactive)
        var createResponse = await client.PostAsJsonAsync("/api/schemas", new
        {
            name = "To Be Activated",
            dataSource = "Mongo",
            database = "sample_airbnb",
            collection = "listingsAndReviews",
            fields = new[]
            {
                new { path = "name", dataType = "String", isSensitive = false, requiresApproval = false, dataOwnerRoles = Array.Empty<string>(), description = "" }
            }
        });
        var created = await createResponse.Content.ReadFromJsonAsync<SchemaDetailDto>();
        var newId = created!.Schema.Id;

        var response = await client.PostAsJsonAsync($"/api/schemas/{newId}/activate", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ActivateDto>();
        Assert.NotNull(result);
        Assert.Equal(newId, result.Id);
        Assert.True(result.IsActiveSource);
    }

    [Fact]
    public async Task Import_schema_from_json()
    {
        var client = Client("usr_owner", "Data Owner / Admin", null);

        var response = await client.PostAsJsonAsync("/api/schemas/import", new
        {
            fieldsJson = "[{\"path\":\"imported_field\",\"dataType\":\"String\",\"isSensitive\":false,\"requiresApproval\":false,\"dataOwnerRoles\":[],\"description\":\"\"}]"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<SchemaDetailDto>();
        Assert.NotNull(created);
        Assert.Equal("Imported Schema", created!.Schema.Name);
        Assert.Single(created.Schema.Fields);
        Assert.Equal("imported_field", created.Schema.Fields[0].Path);
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

    private sealed record SchemaListDto(List<SchemaSummaryDto> Schemas);

    private sealed record SchemaSummaryDto(
        string Id,
        string Name,
        string DataSource,
        int FieldCount,
        bool IsActiveSource,
        int Version,
        string? UpdatedAt,
        string? UpdatedBy);

    private sealed record SchemaDetailDto(SchemaDefinitionDto Schema);

    private sealed record SchemaDefinitionDto(
        string Id,
        string Name,
        string DataSource,
        string Database,
        string Collection,
        List<SchemaFieldDto> Fields,
        bool IsActiveSource,
        int Version,
        string? Description,
        string? CreatedAt,
        string? UpdatedAt,
        string? UpdatedBy);

    private sealed record SchemaFieldDto(
        string Path,
        string DataType,
        bool IsSensitive,
        bool RequiresApproval,
        List<string> DataOwnerRoles,
        string Description);

    private sealed record ActivateDto(string Id, bool IsActiveSource);
}
