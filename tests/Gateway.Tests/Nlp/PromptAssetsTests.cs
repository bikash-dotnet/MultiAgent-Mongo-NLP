using Gateway.Nlp;
using Gateway.Nlp.Llm;
using Gateway.Persistence;
using Gateway.Schemas;

namespace Gateway.Tests.Nlp;

public class PromptAssetsTests
{
    private static PromptAssets Load() => PromptAssets.LoadFromDirectory(
        Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Prompts"));

    [Fact]
    public void BuildPrompt_injects_every_placeholder()
    {
        var prompt = Load().BuildPrompt("average price by market", """{"market":"All"}""", "previous attempt was not json");

        Assert.Contains("average price by market", prompt);
        Assert.Contains("\"market\":\"All\"", prompt);
        Assert.Contains("previous attempt was not json", prompt);
        Assert.Contains("$group", prompt);
        Assert.DoesNotContain("{{utterance}}", prompt);
        Assert.DoesNotContain("{{schema}}", prompt);
    }

    [Fact]
    public void BuildPrompt_omits_error_block_when_no_previous_error()
    {
        var prompt = Load().BuildPrompt("top listings", "{}", null);

        Assert.DoesNotContain("previous attempt", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildPrompt_renders_fields_from_the_active_schema()
    {
        var registry = new SchemaRegistryStore(new InMemoryDocumentStore());
        await registry.CreateAsync(new SchemaDefinition(
            "sch_prompt",
            "prompt",
            SchemaDataSource.Mongo,
            "sample_airbnb",
            "listingsAndReviews",
            [
                new SchemaField("custom.field", SchemaFieldType.Number, true, true, ["Data Owner / Admin"], "a hint"),
                new SchemaField("price", SchemaFieldType.Number)
            ],
            true));

        var provider = new ActiveSchemaProvider(registry);
        await provider.RefreshAsync();

        var assets = PromptAssets.LoadFromDirectory(
            Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Prompts"),
            provider);

        var prompt = assets.BuildPrompt("top listings", "{}", null);

        Assert.Contains("collection: sample_airbnb.listingsAndReviews", prompt);
        Assert.Contains("custom.field: number [sensitive, requires approval] (a hint)", prompt);
        Assert.Contains("price: number", prompt);
    }

    [Fact]
    public void IsConfigured_is_false_for_placeholder_key()
    {
        var options = new NvidiaNimOptions { ApiKey = "TBD", Model = "m" };

        Assert.False(options.IsConfigured);
    }
}
