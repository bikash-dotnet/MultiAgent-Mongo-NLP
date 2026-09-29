using Gateway.Persistence;

namespace Gateway.Tests.Persistence;

public class FileDocumentStoreTests
{
    private sealed record SampleDoc(string Name, int Score);

    [Fact]
    public async Task Upsert_then_get_round_trips()
    {
        var directory = NewDirectory();
        var store = new FileDocumentStore(directory);

        await store.UpsertAsync("things", "a1", new SampleDoc("alpha", 3), CancellationToken.None);
        var loaded = await store.GetAsync<SampleDoc>("things", "a1", CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("alpha", loaded!.Name);
        Assert.Equal(3, loaded.Score);
    }

    [Fact]
    public async Task Upsert_overwrites_and_get_all_returns_every_document()
    {
        var directory = NewDirectory();
        var store = new FileDocumentStore(directory);

        await store.UpsertAsync("things", "a1", new SampleDoc("alpha", 1), CancellationToken.None);
        await store.UpsertAsync("things", "a2", new SampleDoc("beta", 2), CancellationToken.None);
        await store.UpsertAsync("things", "a1", new SampleDoc("alpha2", 9), CancellationToken.None);

        var all = await store.GetAllAsync<SampleDoc>("things", CancellationToken.None);

        Assert.Equal(2, all.Count);
        Assert.Equal("alpha2", all.Single(document => document.Name.StartsWith("alpha")).Name);
    }

    [Fact]
    public async Task Missing_document_returns_null()
    {
        var store = new FileDocumentStore(NewDirectory());

        Assert.Null(await store.GetAsync<SampleDoc>("things", "missing", CancellationToken.None));
    }

    [Fact]
    public async Task A_new_store_instance_reads_what_a_previous_instance_wrote()
    {
        var directory = NewDirectory();

        await new FileDocumentStore(directory).UpsertAsync(
            "things", "a1", new SampleDoc("alpha", 7), CancellationToken.None);

        var reopened = new FileDocumentStore(directory);
        var loaded = await reopened.GetAsync<SampleDoc>("things", "a1", CancellationToken.None);

        Assert.Equal("alpha", loaded!.Name);
    }

    private static string NewDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "gateway-docs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
