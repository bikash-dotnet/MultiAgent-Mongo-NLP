using Gateway.Persistence;

namespace Gateway.Tests.Persistence;

public class DocumentStoreFactoryTests
{
    [Fact]
    public void File_mode_returns_a_file_store()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "gateway-root", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);

        var store = DocumentStoreFactory.Create(
            new PersistenceOptions { Mode = "file", DataDirectory = ".data" },
            contentRoot);

        Assert.IsType<FileDocumentStore>(store);
        Assert.True(Directory.Exists(Path.Combine(contentRoot, ".data")));
    }

    [Fact]
    public void Unknown_mode_defaults_to_the_file_store()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "gateway-root", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);

        var store = DocumentStoreFactory.Create(
            new PersistenceOptions { Mode = "nonsense" },
            contentRoot);

        Assert.IsType<FileDocumentStore>(store);
    }
}
