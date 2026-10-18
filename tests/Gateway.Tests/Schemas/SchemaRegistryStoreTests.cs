using Gateway.Persistence;
using Gateway.Schemas;

namespace Gateway.Tests.Schemas;

public class SchemaRegistryStoreTests
{
    private readonly InMemoryDocumentStore _store = new();
    private readonly SchemaRegistryStore _registry;

    public SchemaRegistryStoreTests()
    {
        _registry = new SchemaRegistryStore(_store);
    }

    [Fact]
    public async Task Seed_is_idempotent()
    {
        await _registry.SeedAsync();
        await _registry.SeedAsync();

        var schemas = await _registry.ListAsync();

        var schema = Assert.Single(schemas);
        Assert.True(schema.IsActiveSource);
        Assert.NotEmpty(schema.Fields);
    }

    [Fact]
    public async Task Activate_is_exclusive()
    {
        await _registry.CreateAsync(NewSchema("sch_one", active: true));
        await _registry.CreateAsync(NewSchema("sch_two"));

        await _registry.ActivateAsync("sch_two");

        var schemas = await _registry.ListAsync();
        Assert.False(schemas.Single(s => s.Id == "sch_one").IsActiveSource);
        Assert.True(schemas.Single(s => s.Id == "sch_two").IsActiveSource);
    }

    [Fact]
    public async Task Save_increments_version()
    {
        var created = await _registry.CreateAsync(NewSchema("sch_version"));
        Assert.Equal(1, created.Version);

        var saved = await _registry.SaveAsync(created with { Name = "Renamed" });
        var reloaded = await _registry.GetAsync("sch_version");

        Assert.Equal(2, saved.Version);
        Assert.Equal(2, reloaded!.Version);
        Assert.Equal("Renamed", reloaded.Name);
    }

    [Fact]
    public async Task Delete_removes_schema()
    {
        await _registry.CreateAsync(NewSchema("sch_delete"));

        var deleted = await _registry.DeleteAsync("sch_delete");

        Assert.True(deleted);
        Assert.Null(await _registry.GetAsync("sch_delete"));
    }

    [Fact]
    public async Task GetActive_returns_only_the_active_schema()
    {
        await _registry.CreateAsync(NewSchema("sch_a"));
        await _registry.CreateAsync(NewSchema("sch_b", active: true));

        var active = await _registry.GetActiveAsync();

        Assert.NotNull(active);
        Assert.Equal("sch_b", active!.Id);
    }

    [Fact]
    public async Task Activate_unknown_schema_throws()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _registry.ActivateAsync("missing"));
    }

    [Fact]
    public async Task Concurrent_activations_leave_exactly_one_active_schema()
    {
        for (var index = 0; index < 8; index++)
        {
            await _registry.CreateAsync(NewSchema($"sch_{index}"));
        }

        var activations = Enumerable.Range(0, 40)
            .Select(index => _registry.ActivateAsync($"sch_{index % 8}"));

        await Task.WhenAll(activations);

        var schemas = await _registry.ListAsync();
        Assert.Single(schemas, schema => schema.IsActiveSource);
    }

    private static SchemaDefinition NewSchema(string id, bool active = false)
    {
        return new SchemaDefinition(
            id,
            id,
            SchemaDataSource.Mongo,
            "db",
            "coll",
            [new SchemaField("price", SchemaFieldType.Number)],
            active);
    }
}
