namespace Gateway.Tests.Nlp;

public class GatewayCsprojTests
{
    [Fact]
    public void Nlp_assets_are_copied_to_build_output()
    {
        var baseDir = AppContext.BaseDirectory;

        Assert.True(File.Exists(Path.Combine(baseDir, "Nlp", "Assets", "Gazetteers", "markets.json")));
        Assert.True(File.Exists(Path.Combine(baseDir, "Nlp", "Assets", "Gazetteers", "amenities.json")));
        Assert.True(File.Exists(Path.Combine(baseDir, "Nlp", "Assets", "Templates", "match.scriban")));
        Assert.True(File.Exists(Path.Combine(baseDir, "Nlp", "Assets", "Templates", "sort.scriban")));
        Assert.True(File.Exists(Path.Combine(baseDir, "Nlp", "Assets", "Templates", "limit.scriban")));
        Assert.True(File.Exists(Path.Combine(baseDir, "Nlp", "Assets", "Prompts", "prompt_template.txt")));
        Assert.True(File.Exists(Path.Combine(baseDir, "Nlp", "Assets", "Prompts", "schema.txt")));
        Assert.True(File.Exists(Path.Combine(baseDir, "Nlp", "Assets", "Prompts", "sample.txt")));
        Assert.True(File.Exists(Path.Combine(baseDir, "Nlp", "Assets", "Prompts", "examples.txt")));
    }
}
