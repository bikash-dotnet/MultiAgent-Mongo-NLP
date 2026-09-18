using Gateway.Nlp.Guardrails;

namespace Gateway.Tests.Nlp;

public class SchemaWhitelistTests
{
    private static readonly SchemaWhitelist Whitelist = SchemaWhitelist.FromFields(
        ["price", "amenities", "address.market", "review_scores.rating"]);

    [Theory]
    [InlineData("price")]
    [InlineData("address.market")]
    [InlineData("_id")]
    public void Known_exact_fields_pass(string field)
    {
        Assert.True(Whitelist.IsKnown(field));
    }

    [Theory]
    [InlineData("address.market.name")]
    [InlineData("address")]
    public void Prefix_related_paths_are_known(string field)
    {
        Assert.True(Whitelist.IsKnown(field));
    }

    [Theory]
    [InlineData("secrets.token")]
    [InlineData("host.identity")]
    public void Unknown_fields_are_rejected(string field)
    {
        Assert.False(Whitelist.IsKnown(field));
        Assert.Contains(field, Whitelist.FindUnknown([field]));
    }

    [Fact]
    public void Loads_declared_fields_from_schema_file()
    {
        var path = Path.Combine(TestPaths.RepoRoot(), "src", "gateway", "Nlp", "Assets", "Prompts", "schema.txt");
        var whitelist = SchemaWhitelist.LoadFromSchemaFile(path);

        Assert.True(whitelist.IsKnown("price"));
        Assert.True(whitelist.IsKnown("address.market"));
        Assert.True(whitelist.IsKnown("review_scores.rating"));
        Assert.False(whitelist.IsKnown("collection"));
        Assert.False(whitelist.IsKnown("fields"));
    }
}
