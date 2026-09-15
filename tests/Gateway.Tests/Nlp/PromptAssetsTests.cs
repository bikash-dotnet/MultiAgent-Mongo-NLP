using Gateway.Nlp.Llm;

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
    public void IsConfigured_is_false_for_placeholder_key()
    {
        var options = new NvidiaNimOptions { ApiKey = "TBD", Model = "m" };

        Assert.False(options.IsConfigured);
    }
}
