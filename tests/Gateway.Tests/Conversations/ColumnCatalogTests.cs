using Gateway.Conversations;

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
}
