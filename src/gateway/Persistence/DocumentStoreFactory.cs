namespace Gateway.Persistence;

public static class DocumentStoreFactory
{
    public static IDocumentStore Create(PersistenceOptions options, string contentRoot)
    {
        var directory = Path.IsPathRooted(options.DataDirectory)
            ? options.DataDirectory
            : Path.Combine(contentRoot, options.DataDirectory);

        return options.Mode.Equals("file", StringComparison.OrdinalIgnoreCase)
            ? new FileDocumentStore(directory)
            : new FileDocumentStore(directory);
    }

    public static IDocumentStore Create(
        PersistenceOptions options,
        string contentRoot,
        string? mongoConnectionString,
        string mongoDatabase)
    {
        if (options.Mode.Equals("mongo", StringComparison.OrdinalIgnoreCase))
        {
            return new MongoDocumentStore(mongoConnectionString ?? string.Empty, mongoDatabase);
        }

        if (!options.Mode.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return Create(options, contentRoot);
        }

        if (string.IsNullOrWhiteSpace(mongoConnectionString) || !CanReachMongo(mongoConnectionString))
        {
            return Create(options, contentRoot);
        }

        return new MongoDocumentStore(mongoConnectionString, mongoDatabase);
    }

    private static bool CanReachMongo(string connectionString)
    {
        try
        {
            var url = new MongoDB.Driver.MongoUrl(connectionString);
            using var client = new MongoDB.Driver.MongoClient(url);
            client.ListDatabaseNames();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
