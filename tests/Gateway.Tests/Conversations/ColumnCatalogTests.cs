using Gateway.Conversations;
using Gateway.Nlp;
using Gateway.Persistence;
using Gateway.Schemas;

namespace Gateway.Tests.Conversations;

public class ColumnCatalogTests
{
    private readonly ColumnCatalog _catalog = new();

    [Fact]
    public void Standard_columns_come_first_and_are_selected()
    {
        var columns = _catalog.Available(null);

        Assert.Equal(6, columns.Count);
        Assert.Equal("name", columns[0].Name);
        Assert.All(columns, column => Assert.True(column.Selected));
    }

    [Fact]
    public void Query_fields_are_appended_without_duplicates()
    {
        var mql = """[{"$match":{"security_deposit":{"$lte":100},"address.market":"New York"}}]""";

        var names = _catalog.Available(mql).Select(column => column.Name).ToList();

        Assert.Contains("security_deposit", names);
        Assert.Single(names.Where(name => name == "address.market"));
        Assert.Equal("name", names[0]);
    }

    [Fact]
    public async Task Active_schema_fields_replace_the_standard_columns()
    {
        var registry = new SchemaRegistryStore(new InMemoryDocumentStore());
        var fields = new List<SchemaField>
        {
            new("price", SchemaFieldType.Number),
            new("address.market", SchemaFieldType.String),
            new("host.host_verifications", SchemaFieldType.Array, true, true, ["Data Owner / Admin"])
        };
        await registry.CreateAsync(new SchemaDefinition(
            "sch_catalog", "catalog", SchemaDataSource.Mongo, "db", "coll", fields, true));

        var provider = new ActiveSchemaProvider(registry);
        await provider.RefreshAsync();
        var catalog = new ColumnCatalog(provider);

        var names = catalog.Available(null).Select(column => column.Name).ToList();

        Assert.Equal(["price", "address.market", "host.host_verifications"], names);
        Assert.DoesNotContain("room_type", names);
        Assert.DoesNotContain("_id", names);
    }
}
