using Gateway.Conversations;
using Gateway.Rules;
using Gateway.Persistence;
using Gateway.Audit;

namespace Gateway.Tests.Rules;

public class ColumnDefinitionServiceTests
{
    private sealed class CatalogStub : IColumnCatalog
    {
        public IReadOnlyList<ColumnOption> Available(string? mql) =>
            [
                new ColumnOption("price", true),
                new ColumnOption("name", true),
                new ColumnOption("market", true)
            ];
    }

    [Fact]
    public async Task Create_assigns_id_and_version_and_next_order()
    {
        var backend = new InMemoryDocumentStore();
        var store = new ColumnDefinitionStore(backend);
        var service = new ColumnDefinitionService(store, new CatalogStub());

        var def = await service.CreateAsync(new ColumnDefinitionRequest
        {
            Field = "price",
            Label = "Price",
            DataType = ColumnDataType.Currency,
            Format = "USD",
            VisibleByDefault = true,
            Exportable = true,
            Sensitive = false
        }, "user-1", CancellationToken.None);

        Assert.NotNull(def.Id);
        Assert.Equal(1, def.Version);
        Assert.Equal("user-1", def.UpdatedBy);
        Assert.Equal(1, def.Order);
        Assert.Equal("price", def.Field);
        Assert.Equal(ColumnDataType.Currency, def.DataType);
        Assert.Equal("USD", def.Format);
    }

    [Fact]
    public async Task Create_rejects_duplicate_field()
    {
        var backend = new InMemoryDocumentStore();
        var store = new ColumnDefinitionStore(backend);
        var service = new ColumnDefinitionService(store, new CatalogStub());

        await service.CreateAsync(new ColumnDefinitionRequest
        {
            Field = "price",
            Label = "Price",
            DataType = ColumnDataType.String,
            VisibleByDefault = true,
            Exportable = true
        }, "user-1", CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new ColumnDefinitionRequest
            {
                Field = "price",
                Label = "Price Again",
                DataType = ColumnDataType.String,
                VisibleByDefault = true,
                Exportable = true
            }, "user-1", CancellationToken.None));

        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task Create_requires_format_for_currency_and_date()
    {
        var backend = new InMemoryDocumentStore();
        var store = new ColumnDefinitionStore(backend);
        var service = new ColumnDefinitionService(store, new CatalogStub());

        var currencyEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new ColumnDefinitionRequest
            {
                Field = "price",
                Label = "Price",
                DataType = ColumnDataType.Currency,
                Format = null,
                VisibleByDefault = true,
                Exportable = true
            }, "user-1", CancellationToken.None));

        Assert.Contains("requires a format", currencyEx.Message);

        var dateEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new ColumnDefinitionRequest
            {
                Field = "created",
                Label = "Created",
                DataType = ColumnDataType.Date,
                Format = null,
                VisibleByDefault = true,
                Exportable = true
            }, "user-1", CancellationToken.None));

        Assert.Contains("requires a format", dateEx.Message);
    }

    [Fact]
    public async Task Update_increments_version_and_requires_matching_version()
    {
        var backend = new InMemoryDocumentStore();
        var store = new ColumnDefinitionStore(backend);
        var service = new ColumnDefinitionService(store, new CatalogStub());

        var created = await service.CreateAsync(new ColumnDefinitionRequest
        {
            Field = "price",
            Label = "Price",
            DataType = ColumnDataType.String,
            VisibleByDefault = true,
            Exportable = true
        }, "user-1", CancellationToken.None);

        var updated = await service.UpdateAsync(created.Id, new ColumnDefinitionRequest
        {
            Field = "price",
            Label = "Unit Price",
            DataType = ColumnDataType.String,
            Format = null,
            Order = 3,
            VisibleByDefault = true,
            Exportable = true,
            Sensitive = false
        }, "user-2", created.Version, CancellationToken.None);

        Assert.Equal(2, updated.Version);
        Assert.Equal("Unit Price", updated.Label);
        Assert.Equal(3, updated.Order);
        Assert.Equal("user-2", updated.UpdatedBy);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(created.Id, new ColumnDefinitionRequest
            {
                Field = "price",
                Label = "Stale",
                DataType = ColumnDataType.String,
                VisibleByDefault = true,
                Exportable = true
            }, "user-2", created.Version, CancellationToken.None));

        Assert.Contains("modified by another client", ex.Message);
    }

    [Fact]
    public async Task Update_throws_key_not_found_for_missing_id()
    {
        var backend = new InMemoryDocumentStore();
        var store = new ColumnDefinitionStore(backend);
        var service = new ColumnDefinitionService(store, new CatalogStub());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateAsync("nonexistent", new ColumnDefinitionRequest
            {
                Field = "price",
                Label = "Price",
                DataType = ColumnDataType.String,
                VisibleByDefault = true,
                Exportable = true
            }, "user-1", 1, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_returns_false_for_missing_id()
    {
        var backend = new InMemoryDocumentStore();
        var store = new ColumnDefinitionStore(backend);
        var service = new ColumnDefinitionService(store, new CatalogStub());

        var deleted = await service.DeleteAsync("nonexistent", CancellationToken.None);
        Assert.False(deleted);
    }

    [Fact]
    public async Task Reorder_assigns_sequential_orders()
    {
        var backend = new InMemoryDocumentStore();
        var store = new ColumnDefinitionStore(backend);
        var service = new ColumnDefinitionService(store, new CatalogStub());

        var a = await service.CreateAsync(new ColumnDefinitionRequest
        {
            Field = "price", Label = "Price", DataType = ColumnDataType.String,
            VisibleByDefault = true, Exportable = true
        }, "user-1", CancellationToken.None);

        var b = await service.CreateAsync(new ColumnDefinitionRequest
        {
            Field = "name", Label = "Name", DataType = ColumnDataType.String,
            VisibleByDefault = true, Exportable = true
        }, "user-1", CancellationToken.None);

        await service.ReorderAsync(new ColumnDefinitionReorderRequest { Ids = [b.Id, a.Id] }, "user-1", CancellationToken.None);

        var all = await service.GetDefinitionsAsync(CancellationToken.None);
        var storedOnly = all.Where(d => d.Id == a.Id || d.Id == b.Id).ToList();
        Assert.Equal(2, storedOnly.Count);
        Assert.Equal(b.Id, storedOnly.First(d => d.Field == "name").Id);
        Assert.Equal(0, storedOnly.First(d => d.Field == "name").Order);
        Assert.Equal(1, storedOnly.First(d => d.Field == "price").Order);
    }

    [Fact]
    public async Task Get_definitions_returns_stored_then_catalog_fallbacks()
    {
        var backend = new InMemoryDocumentStore();
        var store = new ColumnDefinitionStore(backend);
        var service = new ColumnDefinitionService(store, new CatalogStub());

        await service.CreateAsync(new ColumnDefinitionRequest
        {
            Field = "price", Label = "Price", DataType = ColumnDataType.Currency, Format = "USD",
            VisibleByDefault = true, Exportable = true
        }, "user-1", CancellationToken.None);

        var all = await service.GetDefinitionsAsync(CancellationToken.None);

        var price = all.First(d => d.Field == "price");
        Assert.Equal("Price", price.Label);
        Assert.Equal(ColumnDataType.Currency, price.DataType);

        var name = all.First(d => d.Field == "name");
        Assert.Equal("name", name.Label);
        Assert.Equal(ColumnDataType.String, name.DataType);

        var market = all.First(d => d.Field == "market");
        Assert.Equal("market", market.Label);
    }

    [Fact]
    public async Task Create_writes_audit_entry_when_audit_store_provided()
    {
        var backend = new InMemoryDocumentStore();
        var store = new ColumnDefinitionStore(backend);
        var audit = new InMemoryAuditLogStore();
        var service = new ColumnDefinitionService(store, new CatalogStub(), audit);

        await service.CreateAsync(new ColumnDefinitionRequest
        {
            Field = "price", Label = "Price", DataType = ColumnDataType.String,
            VisibleByDefault = true, Exportable = true
        }, "user-1", CancellationToken.None);

        var logs = await audit.ListAsync(CancellationToken.None);
        Assert.Single(logs);
        Assert.Contains("column_definition.create", logs[0].SchemaAction);
        Assert.Contains("created column 'price'", logs[0].Governance.AuthorizedBy);
    }

    [Fact]
    public async Task Update_writes_audit_entry_when_audit_store_provided()
    {
        var backend = new InMemoryDocumentStore();
        var store = new ColumnDefinitionStore(backend);
        var audit = new InMemoryAuditLogStore();
        var service = new ColumnDefinitionService(store, new CatalogStub(), audit);

        var created = await service.CreateAsync(new ColumnDefinitionRequest
        {
            Field = "price", Label = "Price", DataType = ColumnDataType.String,
            VisibleByDefault = true, Exportable = true
        }, "user-1", CancellationToken.None);

        await service.UpdateAsync(created.Id, new ColumnDefinitionRequest
        {
            Field = "price", Label = "Unit Price", DataType = ColumnDataType.String,
            VisibleByDefault = true, Exportable = true
        }, "user-2", created.Version, CancellationToken.None);

        var logs = await audit.ListAsync(CancellationToken.None);
        Assert.Equal(2, logs.Count);
        Assert.Contains("column_definition.update", logs.Select(l => l.SchemaAction));
    }

    [Fact]
    public async Task Delete_writes_audit_entry_when_audit_store_provided()
    {
        var backend = new InMemoryDocumentStore();
        var store = new ColumnDefinitionStore(backend);
        var audit = new InMemoryAuditLogStore();
        var service = new ColumnDefinitionService(store, new CatalogStub(), audit);

        var created = await service.CreateAsync(new ColumnDefinitionRequest
        {
            Field = "price", Label = "Price", DataType = ColumnDataType.String,
            VisibleByDefault = true, Exportable = true
        }, "user-1", CancellationToken.None);

        await service.DeleteAsync(created.Id, CancellationToken.None);

        var logs = await audit.ListAsync(CancellationToken.None);
        Assert.Equal(2, logs.Count);
        Assert.Contains("column_definition.delete", logs.Select(l => l.SchemaAction));
    }

    [Fact]
    public async Task Service_without_audit_store_does_not_throw()
    {
        var backend = new InMemoryDocumentStore();
        var store = new ColumnDefinitionStore(backend);
        var service = new ColumnDefinitionService(store, new CatalogStub());

        var def = await service.CreateAsync(new ColumnDefinitionRequest
        {
            Field = "price", Label = "Price", DataType = ColumnDataType.String,
            VisibleByDefault = true, Exportable = true
        }, "user-1", CancellationToken.None);

        Assert.NotNull(def.Id);
    }
}
