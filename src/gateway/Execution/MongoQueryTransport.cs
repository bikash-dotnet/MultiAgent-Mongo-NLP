using System.Diagnostics;
using System.Globalization;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Microsoft.Extensions.Options;

namespace Gateway.Execution;

public sealed class MongoQueryTransport : IQueryTransport
{
    private static readonly JsonWriterSettings Relaxed = new() { OutputMode = JsonOutputMode.RelaxedExtendedJson };

    private readonly IMongoDatabase _database;
    private readonly ExecutionOptions _options;

    public MongoQueryTransport(string connectionString, IOptions<ExecutionOptions> options)
    {
        var client = new MongoClient(connectionString);
        _options = options.Value;
        _database = client.GetDatabase(_options.Database);
    }

    public async Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var stages = ParseStages(request.Mql);
        var pipeline = PipelineDefinition<BsonDocument, BsonDocument>.Create(stages);
        var collection = _database.GetCollection<BsonDocument>(_options.Collection);
        var stopwatch = Stopwatch.StartNew();

        var documents = await collection.Aggregate(pipeline).ToListAsync(cancellationToken);
        stopwatch.Stop();

        var rows = documents
            .Select(document => (IReadOnlyDictionary<string, string?>)request.Columns.ToDictionary(
                column => column,
                column => Value(document, column),
                StringComparer.Ordinal))
            .ToList();

        return new TabularResult(request.Columns, rows, ExecutionDataSource.Mongo.ToString(), stopwatch.ElapsedMilliseconds);
    }

    private static List<BsonDocument> ParseStages(string mql)
    {
        try
        {
            return BsonSerializer.Deserialize<BsonArray>(mql).Select(value => value.AsBsonDocument).ToList();
        }
        catch (BsonException)
        {
            throw;
        }
        catch (Exception error)
        {
            throw new BsonException("The query pipeline is not valid JSON.", error);
        }
    }

    private static string? Value(BsonDocument document, string path)
    {
        if (!TryGetPath(document, path, out var value) || value is null || value.IsBsonNull)
        {
            return null;
        }

        return value.BsonType switch
        {
            BsonType.String => value.AsString,
            BsonType.Int32 => value.AsInt32.ToString(CultureInfo.InvariantCulture),
            BsonType.Int64 => value.AsInt64.ToString(CultureInfo.InvariantCulture),
            BsonType.Double => value.AsDouble.ToString(CultureInfo.InvariantCulture),
            BsonType.Decimal128 => value.AsDecimal128.ToString(),
            BsonType.Boolean => value.AsBoolean ? "true" : "false",
            BsonType.DateTime => value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
            _ => value.ToJson(Relaxed)
        };
    }

    private static bool TryGetPath(BsonDocument document, string path, out BsonValue? value)
    {
        value = document;
        foreach (var segment in path.Split('.'))
        {
            if (value is not BsonDocument current || !current.TryGetValue(segment, out var next))
            {
                value = null;
                return false;
            }

            value = next;
        }

        return true;
    }
}
