using Gateway.Rules;
using Gateway.Persistence;

namespace Gateway.Tests.Rules;

public class ColumnDefinitionStoreTests
{
    [Fact]
    public async Task Upsert_then_get_round_trips_with_version()
    {
        var backend = new InMemoryDocumentStore();
        var store = new ColumnDefinitionStore(backend);

        var def = new ColumnDefinition(
            ColumnDefinition.NewId(), "price", "Price", ColumnDataType.Currency, "USD",
            1, true, true, false, null,
            1, null, null);

        var stored = await store.UpsertAsync(def, "user-1", CancellationToken.None);

        Assert.Equal(1, stored.Version);
        Assert.Equal("user-1", stored.UpdatedBy);
        Assert.NotNull(stored.UpdatedAt);

        var loaded = await store.GetAsync(stored.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal("price", loaded!.Field);
        Assert.Equal(ColumnDataType.Currency, loaded.DataType);
        Assert.Equal(1, loaded.Version);
    }

    [Fact]
    public async Task Upsert_increments_version()
    {
        var backend = new InMemoryDocumentStore();
        var store = new ColumnDefinitionStore(backend);

        var def = new ColumnDefinition(
            ColumnDefinition.NewId(), "price", "Price", ColumnDataType.Currency, "USD",
            1, true, true, false, null,
            1, null, null);

        var first = await store.UpsertAsync(def, "user-1", CancellationToken.None);
        var second = await store.UpsertAsync(first with { Label = "Unit Price", Order = 5 }, "user-2", CancellationToken.None);

        Assert.Equal(2, second.Version);
        Assert.Equal("user-2", second.UpdatedBy);

        var loaded = await store.GetAsync(first.Id, CancellationToken.None);
        Assert.Equal(2, loaded!.Version);
        Assert.Equal("Unit Price", loaded.Label);
    }

    [Fact]
    public async Task List_returns_all_definitions()
    {
        var backend = new InMemoryDocumentStore();
        var store = new ColumnDefinitionStore(backend);

        await store.UpsertAsync(new ColumnDefinition(
            ColumnDefinition.NewId(), "price", "Price", ColumnDataType.Currency, "USD",
            1, true, true, false, null,
            1, null, null), "user-1", CancellationToken.None);

        await store.UpsertAsync(new ColumnDefinition(
            ColumnDefinition.NewId(), "name", "Name", ColumnDataType.String, null,
            2, true, true, false, null,
            1, null, null), "user-1", CancellationToken.None);

        var all = await store.ListAsync(CancellationToken.None);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task Delete_removes_definition()
    {
        var backend = new InMemoryDocumentStore();
        var store = new ColumnDefinitionStore(backend);

        var def = await store.UpsertAsync(new ColumnDefinition(
            ColumnDefinition.NewId(), "price", "Price", ColumnDataType.Currency, "USD",
            1, true, true, false, null,
            1, null, null), "user-1", CancellationToken.None);

        var deleted = await store.DeleteAsync(def.Id, CancellationToken.None);
        Assert.True(deleted);

        var loaded = await store.GetAsync(def.Id, CancellationToken.None);
        Assert.Null(loaded);

        var missing = await store.DeleteAsync("nonexistent", CancellationToken.None);
        Assert.False(missing);
    }
}
