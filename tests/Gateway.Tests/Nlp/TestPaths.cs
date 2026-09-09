namespace Gateway.Tests.Nlp;

public static class TestPaths
{
    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MultiAgentMongoNlp.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir.FullName;
    }

    public static string ModelRoot()
    {
        return Path.Combine(RepoRoot(), "src", "gateway", "Models", "bge-small-en-v1.5");
    }

    public static string ModelPath()
    {
        return Path.Combine(ModelRoot(), "model_quantized.onnx");
    }

    public static string VocabPath()
    {
        return Path.Combine(ModelRoot(), "vocab.txt");
    }
}
