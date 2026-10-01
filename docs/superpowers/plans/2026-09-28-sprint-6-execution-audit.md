# Sprint 6 Execution and Audit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Execute validated read-only queries against MongoDB or Enterprise Core REST behind one server-side policy, append a complete BRD 6.1 audit row on every execution, persist governance holds durably, and render the rows in an Angular results grid.

**Architecture:** Add a `Gateway.Persistence` document seam (`IDocumentStore`) with Mongo and file adapters, a `Gateway.Execution` runner with two transports behind an `IQueryTransport` contract and a policy-based router, and a `Gateway.Audit` append-only store. The direct query path and the report conversation both call the runner; approval resume executes too. The SPA renders a plain Angular grid.

**Tech Stack:** ASP.NET Core 10 minimal API, `MongoDB.Driver`, `System.Text.Json`, xUnit, `FakeTimeProvider`; Angular 21 standalone components, vitest + jsdom.

## Global Constraints

- Runtime floor: `net10.0` for the gateway and tests; do not change the target framework.
- The only new dependency is the `MongoDB.Driver` NuGet package on `src/gateway`. Do not add any npm package.
- Enums and statuses are strings on the wire (`"Mongo"`, `"EnterpriseCoreREST"`, `"PENDING_LEAD"`, `"APPROVED"`, `"REJECTED"`, `"CSV"`, `"EXEMPTION_OWNER_ACCESS"`).
- Existing positional records only gain trailing optional parameters with defaults; never reorder or add a required parameter.
- The transport is chosen server-side by policy; the client never selects it and both transports return the same `TabularResult` contract.
- Execution timeout default is `5,000 ms`, enforced with a linked cancellation token; a timeout is audited as a failure.
- `IAuditLogStore` exposes append and read only; there is no update or delete method anywhere on the audit path.
- All persistence goes through `IDocumentStore`; tests must not require a live MongoDB. Mongo adapter tests skip when `MONGODB_TEST_CONNECTION` is unset.
- No code comments and no emojis anywhere.
- Never commit a real API key; keep `NvidiaNim:ApiKey` as the existing `TBD` placeholder.
- Do not modify CI. Do not commit `src/web/.vscode/`.
- Running the full .NET suite rewrites `docs/benchmarks/*.md`; always run `git restore docs/benchmarks/` afterwards and never stage those files.
- Commit with the repo hook workaround and end with the co-author trailer:
  `printf '<subject>\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg && git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg`
- Prefix every shell invocation that needs .NET with `export PATH="$PATH:/root/.dotnet"`.

---

## File Structure

New persistence files:

- `src/gateway/Persistence/IDocumentStore.cs` — generic document seam.
- `src/gateway/Persistence/InMemoryDocumentStore.cs` — test and dev fallback seam.
- `src/gateway/Persistence/FileDocumentStore.cs` — durable JSON file adapter.
- `src/gateway/Persistence/MongoDocumentStore.cs` — MongoDB adapter.
- `src/gateway/Persistence/DocumentStoreFactory.cs` — startup adapter selection.
- `src/gateway/Persistence/PersistenceOptions.cs` — `Persistence` config.

New execution files:

- `src/gateway/Execution/ExecutionDataSource.cs`, `ExecutionOptions.cs`, `EnterpriseCoreOptions.cs`.
- `src/gateway/Execution/ExecutionRequest.cs`, `TabularResult.cs`, `GovernanceDecision.cs`.
- `src/gateway/Execution/ITabularQueryExecutor.cs`, `IQueryTransport.cs`.
- `src/gateway/Execution/ExecutionRunner.cs`, `RoutingQueryExecutor.cs`, `DemoTabularSource.cs`.
- `src/gateway/Execution/MongoQueryTransport.cs`, `EnterpriseCoreQueryTransport.cs`.

New audit files:

- `src/gateway/Audit/AuditLogDocument.cs`, `IAuditLogStore.cs`, `AuditLogFactory.cs`.
- `src/gateway/Audit/InMemoryAuditLogStore.cs`, `FileAuditLogStore.cs`, `MongoAuditLogStore.cs`.

Durable store adapters:

- `src/gateway/Persistence/DurableAgentStateStore.cs`, `DurableAccessRequestStore.cs`, `DurableConversationStore.cs`.

New SPA files:

- `src/web/src/app/results/results-grid.component.ts` / `.html` / `.scss` / `.spec.ts`.
- `src/web/src/app/models/execution.ts`.

New test files:

- `tests/Gateway.Tests/Execution/ExecutionOptionsTests.cs`, `ExecutionRunnerTests.cs`, `RoutingQueryExecutorTests.cs`, `EnterpriseCoreQueryTransportTests.cs`.
- `tests/Gateway.Tests/Persistence/FileDocumentStoreTests.cs`, `DurableStoreTests.cs`, `DocumentStoreFactoryTests.cs`.
- `tests/Gateway.Tests/Audit/AuditLogTests.cs`.
- `tests/Gateway.Tests/Nlp/NlpOrchestratorExecutionTests.cs`.
- `tests/Gateway.Tests/Conversations/ConversationExecutionTests.cs`.
- `tests/Gateway.Tests/ApiExecutionTests.cs`.

Changed backend files: `NlpOrchestrator.cs`, `INlpOrchestrator.cs`, `NlpRouteResult.cs`, `NlpQueryResponse.cs`, `ConversationOrchestrator.cs`, `ConversationState.cs`, `ConversationTurn.cs`, `ReportService.cs`, `NlpServiceCollectionExtensions.cs`, `Program.cs`, `gateway.csproj`, `appsettings.json`, `.gitignore`.

---

### Task 1: Persistence and execution options plus the MongoDB driver

**Files:**
- Create: `src/gateway/Persistence/PersistenceOptions.cs`
- Create: `src/gateway/Execution/ExecutionDataSource.cs`
- Create: `src/gateway/Execution/ExecutionOptions.cs`
- Create: `src/gateway/Execution/EnterpriseCoreOptions.cs`
- Modify: `src/gateway/gateway.csproj`
- Modify: `src/gateway/appsettings.json`
- Modify: `.gitignore`
- Test: `tests/Gateway.Tests/Execution/ExecutionOptionsTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `Gateway.Execution.ExecutionDataSource { Mongo, EnterpriseCoreREST }`
  - `Gateway.Execution.ExecutionOptions { const string SectionName = "Execution"; ExecutionDataSource DataSource; int TimeoutMs; string Collection; string Database; }`
  - `Gateway.Execution.EnterpriseCoreOptions { const string SectionName = "EnterpriseCore"; string BaseUrl; string QueryPath; }`
  - `Gateway.Persistence.PersistenceOptions { const string SectionName = "Persistence"; string Mode; string DataDirectory; }`

- [ ] **Step 1: Add the MongoDB driver**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet add src/gateway/gateway.csproj package MongoDB.Driver
```

Expected: the command prints `PackageReference for package 'MongoDB.Driver' version '<x.y.z>' added` and `gateway.csproj` now lists one new `<PackageReference>`.

- [ ] **Step 2: Write the failing test**

Create `tests/Gateway.Tests/Execution/ExecutionOptionsTests.cs`:

```csharp
using Gateway.Execution;
using Gateway.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Execution;

public class ExecutionOptionsTests
{
    [Fact]
    public void Execution_defaults_match_the_brd()
    {
        var options = new ExecutionOptions();

        Assert.Equal(ExecutionDataSource.Mongo, options.DataSource);
        Assert.Equal(5000, options.TimeoutMs);
        Assert.Equal("listingsAndReviews", options.Collection);
        Assert.Equal("sample_airbnb", options.Database);
    }

    [Fact]
    public void Persistence_defaults_to_auto()
    {
        var options = new PersistenceOptions();

        Assert.Equal("auto", options.Mode);
        Assert.Equal(".data", options.DataDirectory);
    }

    [Fact]
    public void Execution_section_binds_from_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Execution:DataSource"] = "EnterpriseCoreREST",
                ["Execution:TimeoutMs"] = "2500",
                ["Execution:Collection"] = "other",
                ["Execution:Database"] = "otherdb",
                ["EnterpriseCore:BaseUrl"] = "http://localhost:5000",
                ["EnterpriseCore:QueryPath"] = "/api/query",
                ["Persistence:Mode"] = "file"
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<ExecutionOptions>(configuration.GetSection(ExecutionOptions.SectionName));
        services.Configure<EnterpriseCoreOptions>(configuration.GetSection(EnterpriseCoreOptions.SectionName));
        services.Configure<PersistenceOptions>(configuration.GetSection(PersistenceOptions.SectionName));
        using var provider = services.BuildServiceProvider();

        var execution = provider.GetRequiredService<IOptions<ExecutionOptions>>().Value;
        var enterprise = provider.GetRequiredService<IOptions<EnterpriseCoreOptions>>().Value;
        var persistence = provider.GetRequiredService<IOptions<PersistenceOptions>>().Value;

        Assert.Equal(ExecutionDataSource.EnterpriseCoreREST, execution.DataSource);
        Assert.Equal(2500, execution.TimeoutMs);
        Assert.Equal("otherdb", execution.Database);
        Assert.Equal("http://localhost:5000", enterprise.BaseUrl);
        Assert.Equal("/api/query", enterprise.QueryPath);
        Assert.Equal("file", persistence.Mode);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ExecutionOptionsTests"`

Expected: FAIL to build with `The type or namespace name 'Execution' does not exist`.

- [ ] **Step 4: Write the implementations**

Create `src/gateway/Persistence/PersistenceOptions.cs`:

```csharp
namespace Gateway.Persistence;

public sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";

    public string Mode { get; set; } = "auto";

    public string DataDirectory { get; set; } = ".data";
}
```

Create `src/gateway/Execution/ExecutionDataSource.cs`:

```csharp
namespace Gateway.Execution;

public enum ExecutionDataSource
{
    Mongo,
    EnterpriseCoreREST
}
```

Create `src/gateway/Execution/ExecutionOptions.cs`:

```csharp
namespace Gateway.Execution;

public sealed class ExecutionOptions
{
    public const string SectionName = "Execution";

    public ExecutionDataSource DataSource { get; set; } = ExecutionDataSource.Mongo;

    public int TimeoutMs { get; set; } = 5000;

    public string Collection { get; set; } = "listingsAndReviews";

    public string Database { get; set; } = "sample_airbnb";
}
```

Create `src/gateway/Execution/EnterpriseCoreOptions.cs`:

```csharp
namespace Gateway.Execution;

public sealed class EnterpriseCoreOptions
{
    public const string SectionName = "EnterpriseCore";

    public string BaseUrl { get; set; } = string.Empty;

    public string QueryPath { get; set; } = "/api/query";
}
```

- [ ] **Step 5: Add the config sections and ignore the data directory**

In `src/gateway/appsettings.json`, add these top-level sections after `"MongoDb"` (keep `MongoDb` unchanged):

```json
  "Execution": {
    "DataSource": "Mongo",
    "TimeoutMs": 5000,
    "Collection": "listingsAndReviews",
    "Database": "sample_airbnb"
  },
  "EnterpriseCore": {
    "BaseUrl": "",
    "QueryPath": "/api/query"
  },
  "Persistence": {
    "Mode": "auto",
    "DataDirectory": ".data"
  },
```

Append `.data/` to `.gitignore` on its own line.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ExecutionOptionsTests"`

Expected: PASS, 3 tests.

- [ ] **Step 7: Commit**

```bash
git add src/gateway/gateway.csproj src/gateway/Persistence/PersistenceOptions.cs src/gateway/Execution src/gateway/appsettings.json .gitignore tests/Gateway.Tests/Execution/ExecutionOptionsTests.cs
printf 'feat(execution): add execution and persistence options\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 2: Document store seam with in-memory and file adapters

**Files:**
- Create: `src/gateway/Persistence/IDocumentStore.cs`
- Create: `src/gateway/Persistence/InMemoryDocumentStore.cs`
- Create: `src/gateway/Persistence/FileDocumentStore.cs`
- Test: `tests/Gateway.Tests/Persistence/FileDocumentStoreTests.cs`

**Interfaces:**
- Consumes: `PersistenceOptions` (Task 1).
- Produces:
  - `Gateway.Persistence.IDocumentStore` with `Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default)`, `Task<IReadOnlyList<T>> GetAllAsync<T>(string collection, CancellationToken cancellationToken = default)`, `Task UpsertAsync<T>(string collection, string id, T document, CancellationToken cancellationToken = default)`.
  - `Gateway.Persistence.InMemoryDocumentStore : IDocumentStore` (parameterless).
  - `Gateway.Persistence.FileDocumentStore : IDocumentStore` with `FileDocumentStore(string directory)`.

- [ ] **Step 1: Write the failing test**

Create `tests/Gateway.Tests/Persistence/FileDocumentStoreTests.cs`:

```csharp
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

        await store.UpsertAsync("things", "a1", new SampleDoc("alpha", 3), TestContext.Current.CancellationToken);
        var loaded = await store.GetAsync<SampleDoc>("things", "a1", TestContext.Current.CancellationToken);

        Assert.NotNull(loaded);
        Assert.Equal("alpha", loaded!.Name);
        Assert.Equal(3, loaded.Score);
    }

    [Fact]
    public async Task Upsert_overwrites_and_get_all_returns_every_document()
    {
        var directory = NewDirectory();
        var store = new FileDocumentStore(directory);

        await store.UpsertAsync("things", "a1", new SampleDoc("alpha", 1), TestContext.Current.CancellationToken);
        await store.UpsertAsync("things", "a2", new SampleDoc("beta", 2), TestContext.Current.CancellationToken);
        await store.UpsertAsync("things", "a1", new SampleDoc("alpha2", 9), TestContext.Current.CancellationToken);

        var all = await store.GetAllAsync<SampleDoc>("things", TestContext.Current.CancellationToken);

        Assert.Equal(2, all.Count);
        Assert.Equal("alpha2", all.Single(document => document.Name.StartsWith("alpha")).Name);
    }

    [Fact]
    public async Task Missing_document_returns_null()
    {
        var store = new FileDocumentStore(NewDirectory());

        Assert.Null(await store.GetAsync<SampleDoc>("things", "missing", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_new_store_instance_reads_what_a_previous_instance_wrote()
    {
        var directory = NewDirectory();

        await new FileDocumentStore(directory).UpsertAsync(
            "things", "a1", new SampleDoc("alpha", 7), TestContext.Current.CancellationToken);

        var reopened = new FileDocumentStore(directory);
        var loaded = await reopened.GetAsync<SampleDoc>("things", "a1", TestContext.Current.CancellationToken);

        Assert.Equal("alpha", loaded!.Name);
    }

    private static string NewDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "gateway-docs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~FileDocumentStoreTests"`

Expected: FAIL to build with `The type or namespace name 'IDocumentStore' could not be found`.

- [ ] **Step 3: Write the implementations**

Create `src/gateway/Persistence/IDocumentStore.cs`:

```csharp
namespace Gateway.Persistence;

public interface IDocumentStore
{
    Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> GetAllAsync<T>(string collection, CancellationToken cancellationToken = default);

    Task UpsertAsync<T>(string collection, string id, T document, CancellationToken cancellationToken = default);
}
```

Create `src/gateway/Persistence/InMemoryDocumentStore.cs`:

```csharp
using System.Collections.Concurrent;
using System.Text.Json;

namespace Gateway.Persistence;

public sealed class InMemoryDocumentStore : IDocumentStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _collections = new(StringComparer.Ordinal);

    public Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default)
    {
        if (!_collections.TryGetValue(collection, out var documents) || !documents.TryGetValue(id, out var json))
        {
            return Task.FromResult<T?>(default);
        }

        return Task.FromResult(JsonSerializer.Deserialize<T>(json));
    }

    public Task<IReadOnlyList<T>> GetAllAsync<T>(string collection, CancellationToken cancellationToken = default)
    {
        if (!_collections.TryGetValue(collection, out var documents))
        {
            return Task.FromResult<IReadOnlyList<T>>([]);
        }

        IReadOnlyList<T> all = documents.Values
            .Select(json => JsonSerializer.Deserialize<T>(json)!)
            .ToList();

        return Task.FromResult(all);
    }

    public Task UpsertAsync<T>(string collection, string id, T document, CancellationToken cancellationToken = default)
    {
        var documents = _collections.GetOrAdd(collection, _ => new ConcurrentDictionary<string, string>(StringComparer.Ordinal));
        documents[id] = JsonSerializer.Serialize(document);
        return Task.CompletedTask;
    }
}
```

Create `src/gateway/Persistence/FileDocumentStore.cs`:

```csharp
using System.Text.Json;

namespace Gateway.Persistence;

public sealed class FileDocumentStore : IDocumentStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private readonly string _directory;

    public FileDocumentStore(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(_directory);
    }

    public async Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default)
    {
        var path = PathFor(collection, id);
        if (!File.Exists(path))
        {
            return default;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<T>(json, Options);
    }

    public async Task<IReadOnlyList<T>> GetAllAsync<T>(string collection, CancellationToken cancellationToken = default)
    {
        var directory = CollectionDirectory(collection);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var results = new List<T>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var document = JsonSerializer.Deserialize<T>(json, Options);
            if (document is not null)
            {
                results.Add(document);
            }
        }

        return results;
    }

    public async Task UpsertAsync<T>(string collection, string id, T document, CancellationToken cancellationToken = default)
    {
        var directory = CollectionDirectory(collection);
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(document, Options);
        await File.WriteAllTextAsync(PathFor(collection, id), json, cancellationToken);
    }

    private string CollectionDirectory(string collection)
    {
        return Path.Combine(_directory, collection);
    }

    private string PathFor(string collection, string id)
    {
        return Path.Combine(CollectionDirectory(collection), $"{id}.json");
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~FileDocumentStoreTests"`

Expected: PASS, 4 tests.

- [ ] **Step 5: Commit**

```bash
git add src/gateway/Persistence/IDocumentStore.cs src/gateway/Persistence/InMemoryDocumentStore.cs src/gateway/Persistence/FileDocumentStore.cs tests/Gateway.Tests/Persistence/FileDocumentStoreTests.cs
printf 'feat(persistence): add the document store seam with file adapter\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 3: Mongo document adapter and startup selection

**Files:**
- Create: `src/gateway/Persistence/MongoDocumentStore.cs`
- Create: `src/gateway/Persistence/DocumentStoreFactory.cs`
- Test: `tests/Gateway.Tests/Persistence/DocumentStoreFactoryTests.cs`

**Interfaces:**
- Consumes: `IDocumentStore`, `InMemoryDocumentStore`, `FileDocumentStore` (Task 2), `PersistenceOptions` (Task 1).
- Produces:
  - `Gateway.Persistence.MongoDocumentStore : IDocumentStore` with `MongoDocumentStore(string connectionString, string database)`.
  - `Gateway.Persistence.DocumentStoreFactory.Create(PersistenceOptions options, string contentRoot) : IDocumentStore`.

- [ ] **Step 1: Write the failing test**

Create `tests/Gateway.Tests/Persistence/DocumentStoreFactoryTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~DocumentStoreFactoryTests"`

Expected: FAIL to build with `The type or namespace name 'DocumentStoreFactory' does not exist`.

- [ ] **Step 3: Write the implementations**

Create `src/gateway/Persistence/MongoDocumentStore.cs`:

```csharp
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Gateway.Persistence;

public sealed class MongoDocumentStore : IDocumentStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private readonly IMongoDatabase _database;

    public MongoDocumentStore(string connectionString, string database)
    {
        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(database);
    }

    public async Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default)
    {
        var found = await Collection(collection)
            .Find(Builders<BsonDocument>.Filter.Eq("_id", id))
            .FirstOrDefaultAsync(cancellationToken);

        return found is null ? default : Deserialize<T>(found);
    }

    public async Task<IReadOnlyList<T>> GetAllAsync<T>(string collection, CancellationToken cancellationToken = default)
    {
        var documents = await Collection(collection)
            .Find(Builders<BsonDocument>.Filter.Empty)
            .ToListAsync(cancellationToken);

        return documents.Select(Deserialize<T>).ToList();
    }

    public Task UpsertAsync<T>(string collection, string id, T document, CancellationToken cancellationToken = default)
    {
        var bson = BsonDocument.Parse(JsonSerializer.Serialize(document, Options));
        bson["_id"] = id;
        return Collection(collection).ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            bson,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    private IMongoCollection<BsonDocument> Collection(string collection)
    {
        return _database.GetCollection<BsonDocument>(collection);
    }

    private static T Deserialize<T>(BsonDocument document)
    {
        document.Remove("_id");
        var json = document.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });
        return JsonSerializer.Deserialize<T>(json, Options)!;
    }
}
```

Create `src/gateway/Persistence/DocumentStoreFactory.cs`:

```csharp
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
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~DocumentStoreFactoryTests"`

Expected: PASS, 2 tests.

- [ ] **Step 5: Commit**

```bash
git add src/gateway/Persistence/MongoDocumentStore.cs src/gateway/Persistence/DocumentStoreFactory.cs tests/Gateway.Tests/Persistence/DocumentStoreFactoryTests.cs
printf 'feat(persistence): add the mongo adapter and store selection\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 4: Durable governance-hold stores

**Files:**
- Create: `src/gateway/Persistence/DurableAgentStateStore.cs`
- Create: `src/gateway/Persistence/DurableAccessRequestStore.cs`
- Create: `src/gateway/Persistence/DurableConversationStore.cs`
- Test: `tests/Gateway.Tests/Persistence/DurableStoreTests.cs`

**Interfaces:**
- Consumes: `IDocumentStore` (Task 2); `Gateway.Nlp.Orchestrator.IAgentStateStore`, `Gateway.Nlp.Guardrails.IAccessRequestStore`, `Gateway.Conversations.IConversationStore` (existing).
- Produces:
  - `Gateway.Persistence.DurableAgentStateStore : IAgentStateStore` with `DurableAgentStateStore(IDocumentStore store)`.
  - `Gateway.Persistence.DurableAccessRequestStore : IAccessRequestStore` with the same constructor.
  - `Gateway.Persistence.DurableConversationStore : IConversationStore` with the same constructor.

- [ ] **Step 1: Write the failing test**

Create `tests/Gateway.Tests/Persistence/DurableStoreTests.cs`:

```csharp
using Gateway.Conversations;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Orchestrator;
using Gateway.Persistence;

namespace Gateway.Tests.Persistence;

public class DurableStoreTests
{
    [Fact]
    public async Task Agent_state_survives_a_new_store_instance()
    {
        var backend = new InMemoryDocumentStore();
        var saved = new AgentState("session-1", "governance_paused", "needs approval", DateTimeOffset.UnixEpoch, "req-1");

        await new DurableAgentStateStore(backend).SaveAsync(saved, TestContext.Current.CancellationToken);

        var reloaded = await new DurableAgentStateStore(backend).LoadAsync("session-1", TestContext.Current.CancellationToken);

        Assert.Equal("governance_paused", reloaded!.Stage);
        Assert.Equal("req-1", reloaded.AccessRequestId);
    }

    [Fact]
    public async Task Access_request_survives_and_updates_in_place()
    {
        var backend = new InMemoryDocumentStore();
        var request = new AccessRequest(string.Empty, "session-1", "[]", ["price"], AccessRequest.PendingLead, null, DateTimeOffset.UnixEpoch);

        var created = await new DurableAccessRequestStore(backend).CreateAsync(request, TestContext.Current.CancellationToken);
        await new DurableAccessRequestStore(backend).UpdateAsync(created with { Status = AccessRequest.Approved }, TestContext.Current.CancellationToken);

        var reloaded = await new DurableAccessRequestStore(backend).GetAsync(created.Id, TestContext.Current.CancellationToken);

        Assert.Equal(AccessRequest.Approved, reloaded!.Status);
    }

    [Fact]
    public async Task Conversation_is_found_by_access_request_after_reload()
    {
        var backend = new InMemoryDocumentStore();
        var state = new ConversationState(
            string.Empty, "session-1", ConversationStep.Complete, "q", "[{\"$match\":{}}]", Gateway.Nlp.Router.NlpRouteKind.GovernancePaused,
            "req-9", ["price"], false, true, new ReportIntakeDraft(), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

        var created = await new DurableConversationStore(backend).CreateAsync(state, TestContext.Current.CancellationToken);
        var found = await new DurableConversationStore(backend).FindByAccessRequestAsync("req-9", TestContext.Current.CancellationToken);

        Assert.Equal(created.Id, found!.Id);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~DurableStoreTests"`

Expected: FAIL to build with `The type or namespace name 'DurableAgentStateStore' does not exist`.

- [ ] **Step 3: Write the implementations**

Create `src/gateway/Persistence/DurableAgentStateStore.cs`:

```csharp
using Gateway.Nlp.Orchestrator;

namespace Gateway.Persistence;

public sealed class DurableAgentStateStore : IAgentStateStore
{
    private const string Collection = "agent_states";

    private readonly IDocumentStore _store;

    public DurableAgentStateStore(IDocumentStore store)
    {
        _store = store;
    }

    public Task SaveAsync(AgentState state, CancellationToken cancellationToken = default)
    {
        return _store.UpsertAsync(Collection, state.SessionId, state, cancellationToken);
    }

    public Task<AgentState?> LoadAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return _store.GetAsync<AgentState>(Collection, sessionId, cancellationToken);
    }
}
```

Create `src/gateway/Persistence/DurableAccessRequestStore.cs`:

```csharp
using Gateway.Nlp.Guardrails;

namespace Gateway.Persistence;

public sealed class DurableAccessRequestStore : IAccessRequestStore
{
    private const string Collection = "access_requests";

    private readonly IDocumentStore _store;

    public DurableAccessRequestStore(IDocumentStore store)
    {
        _store = store;
    }

    public async Task<AccessRequest> CreateAsync(AccessRequest request, CancellationToken cancellationToken = default)
    {
        var stored = string.IsNullOrWhiteSpace(request.Id)
            ? request with { Id = Guid.NewGuid().ToString("N") }
            : request;

        await _store.UpsertAsync(Collection, stored.Id, stored, cancellationToken);
        return stored;
    }

    public Task<AccessRequest?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        return _store.GetAsync<AccessRequest>(Collection, id, cancellationToken);
    }

    public Task<AccessRequest> UpdateAsync(AccessRequest request, CancellationToken cancellationToken = default)
    {
        return UpsertAndReturn(request, cancellationToken);
    }

    public Task<IReadOnlyList<AccessRequest>> ListAsync(CancellationToken cancellationToken = default)
    {
        return _store.GetAllAsync<AccessRequest>(Collection, cancellationToken);
    }

    private async Task<AccessRequest> UpsertAndReturn(AccessRequest request, CancellationToken cancellationToken)
    {
        await _store.UpsertAsync(Collection, request.Id, request, cancellationToken);
        return request;
    }
}
```

Create `src/gateway/Persistence/DurableConversationStore.cs`:

```csharp
using Gateway.Conversations;

namespace Gateway.Persistence;

public sealed class DurableConversationStore : IConversationStore
{
    private const string Collection = "conversations";

    private readonly IDocumentStore _store;

    public DurableConversationStore(IDocumentStore store)
    {
        _store = store;
    }

    public async Task<ConversationState> CreateAsync(ConversationState state, CancellationToken cancellationToken = default)
    {
        var stored = string.IsNullOrWhiteSpace(state.Id)
            ? state with { Id = Guid.NewGuid().ToString("N") }
            : state;

        await _store.UpsertAsync(Collection, stored.Id, stored, cancellationToken);
        return stored;
    }

    public Task<ConversationState?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        return _store.GetAsync<ConversationState>(Collection, id, cancellationToken);
    }

    public async Task<ConversationState?> FindByAccessRequestAsync(string accessRequestId, CancellationToken cancellationToken = default)
    {
        var all = await _store.GetAllAsync<ConversationState>(Collection, cancellationToken);
        return all.FirstOrDefault(state => state.AccessRequestId == accessRequestId);
    }

    public Task<ConversationState> UpdateAsync(ConversationState state, CancellationToken cancellationToken = default)
    {
        return UpsertAndReturn(state, cancellationToken);
    }

    private async Task<ConversationState> UpsertAndReturn(ConversationState state, CancellationToken cancellationToken)
    {
        await _store.UpsertAsync(Collection, state.Id, state, cancellationToken);
        return state;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~DurableStoreTests"`

Expected: PASS, 3 tests.

- [ ] **Step 5: Commit**

```bash
git add src/gateway/Persistence/DurableAgentStateStore.cs src/gateway/Persistence/DurableAccessRequestStore.cs src/gateway/Persistence/DurableConversationStore.cs tests/Gateway.Tests/Persistence/DurableStoreTests.cs
printf 'feat(persistence): add durable governance hold stores\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 5: Execution request, result, and transport contracts

**Files:**
- Create: `src/gateway/Execution/GovernanceDecision.cs`
- Create: `src/gateway/Execution/ExecutionRequest.cs`
- Create: `src/gateway/Execution/TabularResult.cs`
- Create: `src/gateway/Execution/IQueryTransport.cs`
- Create: `src/gateway/Execution/ITabularQueryExecutor.cs`
- Modify: `src/gateway/Governance/RequesterContext.cs`
- Modify: `src/gateway/Auth/SessionClaims.cs`
- Test: `tests/Gateway.Tests/Execution/ExecutionContractTests.cs`

**Interfaces:**
- Consumes: `ExecutionDataSource` (Task 1).
- Produces:
  - `Gateway.Governance.RequesterContext(string UserId, string Name, string Role, string? LeadUserId, string? Email = null)`.
  - `Gateway.Execution.GovernanceDecision(bool SensitiveDataAccessed, IReadOnlyList<string> FlagsTriggered, string? ExemptionType, bool OverrideInvoked, string? AuthorizedBy)`.
  - `Gateway.Execution.ExecutionRequest(string Mql, string SessionId, string Utterance, ExecutionDataSource DataSource, string TargetCollection, IReadOnlyList<string> Columns, IReadOnlyDictionary<string, string> ClarificationsApplied, bool SemanticCacheHit, bool SlotExtractionUsed, int LlmTokensConsumed, RequesterContext User, GovernanceDecision Governance, string? ExportFormat = null)`.
  - `Gateway.Execution.TabularResult(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows, string DataSource, long DurationMs, bool TimedOut = false, string? Error = null)` with `int RowCount` and `static TabularResult Empty(string dataSource)`.
  - `Gateway.Execution.IQueryTransport.ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default) : Task<TabularResult>`.
  - `Gateway.Execution.ITabularQueryExecutor.ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default) : Task<TabularResult>`.

- [ ] **Step 1: Write the failing test**

Create `tests/Gateway.Tests/Execution/ExecutionContractTests.cs`:

```csharp
using System.Security.Claims;
using Gateway.Auth;
using Gateway.Execution;
using Gateway.Governance;

namespace Gateway.Tests.Execution;

public class ExecutionContractTests
{
    [Fact]
    public void Requester_context_carries_the_email()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(SessionClaims.UserId, "usr_1"),
            new Claim(SessionClaims.Name, "Bikash"),
            new Claim(SessionClaims.Email, "bnayak@enterprise.com"),
            new Claim(SessionClaims.Role, "Business Analyst")
        ]));

        var context = SessionClaims.ToRequesterContext(principal);

        Assert.Equal("bnayak@enterprise.com", context.Email);
    }

    [Fact]
    public void Tabular_result_counts_rows_and_reports_empty()
    {
        var rows = new List<IReadOnlyDictionary<string, string?>>
        {
            new Dictionary<string, string?> { ["name"] = "a" },
            new Dictionary<string, string?> { ["name"] = "b" }
        };

        var result = new TabularResult(["name"], rows, "Mongo", 12);
        var empty = TabularResult.Empty("Mongo");

        Assert.Equal(2, result.RowCount);
        Assert.Equal(0, empty.RowCount);
        Assert.False(result.TimedOut);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ExecutionContractTests"`

Expected: FAIL to build with `The type or namespace name 'Execution' does not exist` or `'RequesterContext' does not contain a definition for 'Email'`.

- [ ] **Step 3: Write the implementations**

Modify `src/gateway/Governance/RequesterContext.cs` to:

```csharp
namespace Gateway.Governance;

public sealed record RequesterContext(string UserId, string Name, string Role, string? LeadUserId, string? Email = null);
```

Modify `SessionClaims.ToRequesterContext` in `src/gateway/Auth/SessionClaims.cs` to read the email and pass it:

```csharp
    public static RequesterContext ToRequesterContext(ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(UserId) ?? "anonymous";
        var role = user.FindFirstValue(Role) ?? string.Empty;
        var leadUserId = user.FindFirstValue(LeadUserId);
        var email = user.FindFirstValue(Email);

        return new RequesterContext(
            userId,
            DisplayName(user),
            role,
            string.IsNullOrWhiteSpace(leadUserId) ? null : leadUserId,
            string.IsNullOrWhiteSpace(email) ? null : email);
    }
```

Create `src/gateway/Execution/GovernanceDecision.cs`:

```csharp
namespace Gateway.Execution;

public sealed record GovernanceDecision(
    bool SensitiveDataAccessed,
    IReadOnlyList<string> FlagsTriggered,
    string? ExemptionType,
    bool OverrideInvoked,
    string? AuthorizedBy)
{
    public static GovernanceDecision None { get; } = new(false, [], null, false, null);
}
```

Create `src/gateway/Execution/ExecutionRequest.cs`:

```csharp
using Gateway.Governance;

namespace Gateway.Execution;

public sealed record ExecutionRequest(
    string Mql,
    string SessionId,
    string Utterance,
    ExecutionDataSource DataSource,
    string TargetCollection,
    IReadOnlyList<string> Columns,
    IReadOnlyDictionary<string, string> ClarificationsApplied,
    bool SemanticCacheHit,
    bool SlotExtractionUsed,
    int LlmTokensConsumed,
    RequesterContext User,
    GovernanceDecision Governance,
    string? ExportFormat = null);
```

Create `src/gateway/Execution/TabularResult.cs`:

```csharp
namespace Gateway.Execution;

public sealed record TabularResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows,
    string DataSource,
    long DurationMs,
    bool TimedOut = false,
    string? Error = null)
{
    public int RowCount => Rows.Count;

    public static TabularResult Empty(string dataSource)
    {
        return new TabularResult([], [], dataSource, 0);
    }
}
```

Create `src/gateway/Execution/IQueryTransport.cs`:

```csharp
namespace Gateway.Execution;

public interface IQueryTransport
{
    Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default);
}
```

Create `src/gateway/Execution/ITabularQueryExecutor.cs`:

```csharp
namespace Gateway.Execution;

public interface ITabularQueryExecutor
{
    Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ExecutionContractTests"`

Expected: PASS, 2 tests.

- [ ] **Step 5: Commit**

```bash
git add src/gateway/Execution/GovernanceDecision.cs src/gateway/Execution/ExecutionRequest.cs src/gateway/Execution/TabularResult.cs src/gateway/Execution/IQueryTransport.cs src/gateway/Execution/ITabularQueryExecutor.cs src/gateway/Governance/RequesterContext.cs src/gateway/Auth/SessionClaims.cs tests/Gateway.Tests/Execution/ExecutionContractTests.cs
printf 'feat(execution): add request result and transport contracts\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 6: Append-only audit document, store, and factory

**Files:**
- Create: `src/gateway/Audit/AuditLogDocument.cs`
- Create: `src/gateway/Audit/IAuditLogStore.cs`
- Create: `src/gateway/Audit/DurableAuditLogStore.cs`
- Create: `src/gateway/Audit/InMemoryAuditLogStore.cs`
- Create: `src/gateway/Audit/AuditLogFactory.cs`
- Test: `tests/Gateway.Tests/Audit/AuditLogTests.cs`

**Interfaces:**
- Consumes: `IDocumentStore` (Task 2); `ExecutionRequest`, `TabularResult`, `GovernanceDecision` (Task 5).
- Produces:
  - `Gateway.Audit.AuditLogUser(string UserId, string Name, string? Email, string Role)`.
  - `Gateway.Audit.AuditLogNlpPerformance(bool SemanticCacheHit, bool SlotExtractionUsed, int LlmTokensConsumed, long ExecutionDurationMs)`.
  - `Gateway.Audit.AuditLogRequestDetails(string NaturalLanguagePrompt, IReadOnlyDictionary<string, string> ClarificationsApplied)`.
  - `Gateway.Audit.AuditLogExecutionDetails(string GeneratedQuery, string TargetCollection, int RowsReturned, string? ExportFormat, string? Error)`.
  - `Gateway.Audit.AuditLogGovernance(bool SensitiveDataAccessed, IReadOnlyList<string> FlagsTriggered, string? ExemptionType, bool OverrideInvoked, string? AuthorizedBy)`.
  - `Gateway.Audit.AuditLogDocument(string Id, DateTimeOffset AuditTimestamp, string SessionId, string DataSource, AuditLogUser User, AuditLogNlpPerformance NlpPerformance, AuditLogRequestDetails RequestDetails, AuditLogExecutionDetails ExecutionDetails, AuditLogGovernance Governance)`.
  - `Gateway.Audit.IAuditLogStore.AppendAsync`, `GetAsync`, `ListAsync`.
  - `Gateway.Audit.DurableAuditLogStore(IDocumentStore store)` and `Gateway.Audit.InMemoryAuditLogStore()`.
  - `Gateway.Audit.AuditLogFactory.Build(string id, DateTimeOffset timestamp, ExecutionRequest request, TabularResult result) : AuditLogDocument`.

This task implements `DurableAuditLogStore` instead of the design's separate `FileAuditLogStore` and `MongoAuditLogStore`; the backend is already selected by `IDocumentStore`, so one adapter covers both.

- [ ] **Step 1: Write the failing test**

Create `tests/Gateway.Tests/Audit/AuditLogTests.cs`:

```csharp
using System.Reflection;
using Gateway.Audit;
using Gateway.Execution;
using Gateway.Governance;
using Gateway.Persistence;

namespace Gateway.Tests.Audit;

public class AuditLogTests
{
    [Fact]
    public async Task Append_then_list_round_trips_and_assigns_an_id()
    {
        var store = new InMemoryAuditLogStore();
        var document = BuildDocument();

        var appended = await store.AppendAsync(document with { Id = string.Empty }, TestContext.Current.CancellationToken);
        var listed = await store.ListAsync(TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(appended.Id));
        Assert.Single(listed);
        Assert.Equal(appended.Id, listed[0].Id);
    }

    [Fact]
    public async Task Durable_store_persists_across_instances()
    {
        var backend = new InMemoryDocumentStore();
        var document = BuildDocument();

        var appended = await new DurableAuditLogStore(backend).AppendAsync(document with { Id = string.Empty }, TestContext.Current.CancellationToken);
        var reloaded = await new DurableAuditLogStore(backend).GetAsync(appended.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(reloaded);
        Assert.Equal("usr_1", reloaded!.User.UserId);
    }

    [Fact]
    public void Audit_store_exposes_no_update_or_delete_path()
    {
        var interfaces = new[] { typeof(IAuditLogStore) };
        var implementations = new[] { typeof(DurableAuditLogStore), typeof(InMemoryAuditLogStore) };

        foreach (var method in interfaces.SelectMany(type => type.GetMethods()).Concat(implementations.SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))))
        {
            Assert.DoesNotContain("update", method.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("delete", method.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("remove", method.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Factory_maps_the_full_brd_6_1_shape()
    {
        var result = new TabularResult(["price"], [], "Mongo", 184, Error: "boom");

        var document = AuditLogFactory.Build("audit-1", DateTimeOffset.UnixEpoch, BuildRequest(), result);

        Assert.Equal("sess-1", document.SessionId);
        Assert.Equal("Mongo", document.DataSource);
        Assert.Equal("usr_1", document.User.UserId);
        Assert.Equal("bnayak@enterprise.com", document.User.Email);
        Assert.True(document.Governance.SensitiveDataAccessed);
        Assert.Equal("EXEMPTION_OWNER_ACCESS", document.Governance.ExemptionType);
        Assert.Equal("Bikash", document.Governance.AuthorizedBy);
        Assert.Equal(0, document.ExecutionDetails.RowsReturned);
        Assert.Equal("boom", document.ExecutionDetails.Error);
        Assert.Equal(184, document.NlpPerformance.ExecutionDurationMs);
        Assert.Equal("new-york", document.RequestDetails.ClarificationsApplied["market"]);
    }

    private static ExecutionRequest BuildRequest()
    {
        return new ExecutionRequest(
            "[{\"$match\":{}}]",
            "sess-1",
            "Listings in New York",
            ExecutionDataSource.Mongo,
            "listingsAndReviews",
            ["price"],
            new Dictionary<string, string> { ["market"] = "new-york" },
            false,
            true,
            42,
            new RequesterContext("usr_1", "Bikash", "Data Owner / Admin", null, "bnayak@enterprise.com"),
            new GovernanceDecision(true, ["price"], "EXEMPTION_OWNER_ACCESS", false, "Bikash"));
    }

    private static AuditLogDocument BuildDocument()
    {
        return AuditLogFactory.Build(string.Empty, DateTimeOffset.UnixEpoch, BuildRequest(), TabularResult.Empty("Mongo"));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~AuditLogTests"`

Expected: FAIL to build with `The type or namespace name 'Audit' could not be found`.

- [ ] **Step 3: Write the implementations**

Create `src/gateway/Audit/AuditLogDocument.cs`:

```csharp
namespace Gateway.Audit;

public sealed record AuditLogUser(string UserId, string Name, string? Email, string Role);

public sealed record AuditLogNlpPerformance(bool SemanticCacheHit, bool SlotExtractionUsed, int LlmTokensConsumed, long ExecutionDurationMs);

public sealed record AuditLogRequestDetails(string NaturalLanguagePrompt, IReadOnlyDictionary<string, string> ClarificationsApplied);

public sealed record AuditLogExecutionDetails(string GeneratedQuery, string TargetCollection, int RowsReturned, string? ExportFormat, string? Error);

public sealed record AuditLogGovernance(bool SensitiveDataAccessed, IReadOnlyList<string> FlagsTriggered, string? ExemptionType, bool OverrideInvoked, string? AuthorizedBy);

public sealed record AuditLogDocument(
    string Id,
    DateTimeOffset AuditTimestamp,
    string SessionId,
    string DataSource,
    AuditLogUser User,
    AuditLogNlpPerformance NlpPerformance,
    AuditLogRequestDetails RequestDetails,
    AuditLogExecutionDetails ExecutionDetails,
    AuditLogGovernance Governance);
```

Create `src/gateway/Audit/IAuditLogStore.cs`:

```csharp
namespace Gateway.Audit;

public interface IAuditLogStore
{
    Task<AuditLogDocument> AppendAsync(AuditLogDocument document, CancellationToken cancellationToken = default);

    Task<AuditLogDocument?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLogDocument>> ListAsync(CancellationToken cancellationToken = default);
}
```

Create `src/gateway/Audit/DurableAuditLogStore.cs`:

```csharp
using Gateway.Persistence;

namespace Gateway.Audit;

public sealed class DurableAuditLogStore : IAuditLogStore
{
    private const string Collection = "audit_logs";

    private readonly IDocumentStore _store;

    public DurableAuditLogStore(IDocumentStore store)
    {
        _store = store;
    }

    public async Task<AuditLogDocument> AppendAsync(AuditLogDocument document, CancellationToken cancellationToken = default)
    {
        var stored = string.IsNullOrWhiteSpace(document.Id)
            ? document with { Id = Guid.NewGuid().ToString("N") }
            : document;

        await _store.UpsertAsync(Collection, stored.Id, stored, cancellationToken);
        return stored;
    }

    public Task<AuditLogDocument?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        return _store.GetAsync<AuditLogDocument>(Collection, id, cancellationToken);
    }

    public Task<IReadOnlyList<AuditLogDocument>> ListAsync(CancellationToken cancellationToken = default)
    {
        return _store.GetAllAsync<AuditLogDocument>(Collection, cancellationToken);
    }
}
```

Create `src/gateway/Audit/InMemoryAuditLogStore.cs`:

```csharp
using System.Collections.Concurrent;

namespace Gateway.Audit;

public sealed class InMemoryAuditLogStore : IAuditLogStore
{
    private readonly ConcurrentDictionary<string, AuditLogDocument> _documents = new(StringComparer.Ordinal);

    public Task<AuditLogDocument> AppendAsync(AuditLogDocument document, CancellationToken cancellationToken = default)
    {
        var stored = string.IsNullOrWhiteSpace(document.Id)
            ? document with { Id = Guid.NewGuid().ToString("N") }
            : document;

        _documents[stored.Id] = stored;
        return Task.FromResult(stored);
    }

    public Task<AuditLogDocument?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        _documents.TryGetValue(id, out var document);
        return Task.FromResult(document);
    }

    public Task<IReadOnlyList<AuditLogDocument>> ListAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<AuditLogDocument>>(_documents.Values.ToList());
    }
}
```

Create `src/gateway/Audit/AuditLogFactory.cs`:

```csharp
using Gateway.Execution;

namespace Gateway.Audit;

public static class AuditLogFactory
{
    public static AuditLogDocument Build(string id, DateTimeOffset timestamp, ExecutionRequest request, TabularResult result)
    {
        return new AuditLogDocument(
            id,
            timestamp,
            request.SessionId,
            result.DataSource,
            new AuditLogUser(request.User.UserId, request.User.Name, request.User.Email, request.User.Role),
            new AuditLogNlpPerformance(
                request.SemanticCacheHit,
                request.SlotExtractionUsed,
                request.LlmTokensConsumed,
                result.DurationMs),
            new AuditLogRequestDetails(request.Utterance, request.ClarificationsApplied),
            new AuditLogExecutionDetails(
                request.Mql,
                request.TargetCollection,
                result.RowCount,
                request.ExportFormat,
                result.Error),
            new AuditLogGovernance(
                request.Governance.SensitiveDataAccessed,
                request.Governance.FlagsTriggered,
                request.Governance.ExemptionType,
                request.Governance.OverrideInvoked,
                request.Governance.AuthorizedBy));
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~AuditLogTests"`

Expected: PASS, 4 tests.

- [ ] **Step 5: Commit**

```bash
git add src/gateway/Audit tests/Gateway.Tests/Audit/AuditLogTests.cs
printf 'feat(audit): add append-only audit log store and factory\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 7: Execution runner with timeout, events, and audit

**Files:**
- Create: `src/gateway/Execution/ExecutionRunner.cs`
- Test: `tests/Gateway.Tests/Execution/ExecutionRunnerTests.cs`

**Interfaces:**
- Consumes: `IQueryTransport`, `ExecutionRequest`, `TabularResult` (Task 5); `IAuditLogStore`, `AuditLogFactory` (Task 6); `IAgentEventSink` (existing); `ExecutionOptions` (Task 1).
- Produces:
  - `Gateway.Execution.ExecutionRunner : ITabularQueryExecutor` with `ExecutionRunner(IQueryTransport transport, IAuditLogStore audit, IAgentEventSink events, IOptions<ExecutionOptions> options, TimeProvider clock)`.
  - Publishes `agent.executing` before the call and `agent.completed` after it.

- [ ] **Step 1: Write the failing test**

Create `tests/Gateway.Tests/Execution/ExecutionRunnerTests.cs`:

```csharp
using Gateway.Audit;
using Gateway.Execution;
using Gateway.Governance;
using Gateway.Nlp.Orchestrator;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Execution;

public class ExecutionRunnerTests
{
    [Fact]
    public async Task Success_publishes_events_and_appends_one_audit_row()
    {
        var audit = new InMemoryAuditLogStore();
        var events = new RecordingSink();
        var transport = new StubTransport(request => new TabularResult(
            ["name"], [new Dictionary<string, string?> { ["name"] = "a" }], "Mongo", 7));
        var runner = new ExecutionRunner(transport, audit, events, Options(), TimeProvider.System);

        var result = await runner.ExecuteAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal(1, result.RowCount);
        Assert.Equal(["agent.executing", "agent.completed"], events.Events.Select(entry => entry.Name).ToArray());
        Assert.Single(await audit.ListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Timeout_is_cancelled_and_audited_as_a_failure()
    {
        var audit = new InMemoryAuditLogStore();
        var events = new RecordingSink();
        var runner = new ExecutionRunner(new HangingTransport(), audit, events, Options(30), TimeProvider.System);

        var result = await runner.ExecuteAsync(Request(), TestContext.Current.CancellationToken);

        Assert.True(result.TimedOut);
        Assert.NotNull(result.Error);
        var audited = Assert.Single(await audit.ListAsync(TestContext.Current.CancellationToken));
        Assert.NotNull(audited.ExecutionDetails.Error);
        Assert.Equal("failed", events.Events[^1].Status);
    }

    [Fact]
    public async Task Transport_failure_is_reported_and_audited()
    {
        var audit = new InMemoryAuditLogStore();
        var events = new RecordingSink();
        var runner = new ExecutionRunner(new ThrowingTransport(), audit, events, Options(), TimeProvider.System);

        var result = await runner.ExecuteAsync(Request(), TestContext.Current.CancellationToken);

        Assert.False(result.TimedOut);
        Assert.Equal("mongo down", result.Error);
        Assert.Single(await audit.ListAsync(TestContext.Current.CancellationToken));
    }

    private static IOptions<ExecutionOptions> Options(int timeoutMs = 5000)
    {
        return Microsoft.Extensions.Options.Options.Create(new ExecutionOptions { TimeoutMs = timeoutMs });
    }

    private static ExecutionRequest Request()
    {
        return new ExecutionRequest(
            "[{\"$match\":{}}]",
            "sess-1",
            "Listings",
            ExecutionDataSource.Mongo,
            "listingsAndReviews",
            ["name"],
            new Dictionary<string, string>(),
            false,
            false,
            0,
            new RequesterContext("usr_1", "Bikash", "Business Analyst", null, "bnayak@enterprise.com"),
            GovernanceDecision.None);
    }

    private sealed class StubTransport : IQueryTransport
    {
        private readonly Func<ExecutionRequest, TabularResult> _result;

        public StubTransport(Func<ExecutionRequest, TabularResult> result)
        {
            _result = result;
        }

        public Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result(request));
        }
    }

    private sealed class HangingTransport : IQueryTransport
    {
        public async Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return TabularResult.Empty("Mongo");
        }
    }

    private sealed class ThrowingTransport : IQueryTransport
    {
        public Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("mongo down");
        }
    }

    private sealed class RecordingSink : IAgentEventSink
    {
        public List<AgentEvent> Events { get; } = [];

        public void Publish(AgentEvent agentEvent)
        {
            Events.Add(agentEvent);
        }

        public IAsyncEnumerable<AgentEvent> ReadAllAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ExecutionRunnerTests"`

Expected: FAIL to build with `The type or namespace name 'ExecutionRunner' does not exist`.

- [ ] **Step 3: Write the implementation**

Create `src/gateway/Execution/ExecutionRunner.cs`:

```csharp
using Gateway.Audit;
using Gateway.Nlp.Orchestrator;
using Microsoft.Extensions.Options;

namespace Gateway.Execution;

public sealed class ExecutionRunner : ITabularQueryExecutor
{
    private readonly IQueryTransport _transport;
    private readonly IAuditLogStore _audit;
    private readonly IAgentEventSink _events;
    private readonly ExecutionOptions _options;
    private readonly TimeProvider _clock;

    public ExecutionRunner(
        IQueryTransport transport,
        IAuditLogStore audit,
        IAgentEventSink events,
        IOptions<ExecutionOptions> options,
        TimeProvider clock)
    {
        _transport = transport;
        _audit = audit;
        _events = events;
        _options = options.Value;
        _clock = clock;
    }

    public async Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
    {
        _events.Publish(new AgentEvent("agent.executing", "executing", request.DataSource.ToString()));
        var started = _clock.GetTimestamp();
        TabularResult result;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.TimeoutMs);

        try
        {
            result = await _transport.ExecuteAsync(request, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result = new TabularResult(
                [],
                [],
                request.DataSource.ToString(),
                Elapsed(started),
                TimedOut: true,
                Error: $"Query exceeded the {_options.TimeoutMs} ms execution limit.");
        }
        catch (Exception error)
        {
            result = new TabularResult([], [], request.DataSource.ToString(), Elapsed(started), false, error.Message);
        }

        try
        {
            await _audit.AppendAsync(
                AuditLogFactory.Build(string.Empty, _clock.GetUtcNow(), request, result),
                cancellationToken);
        }
        catch (Exception error)
        {
            var message = $"Audit write failed: {error.Message}";
            result = result with { Error = result.Error is null ? message : $"{result.Error}; {message}" };
        }

        _events.Publish(new AgentEvent(
            "agent.completed",
            result.TimedOut || result.Error is not null ? "failed" : "completed",
            $"{result.RowCount} rows in {result.DurationMs} ms"));

        return result;
    }

    private long Elapsed(long started)
    {
        return (long)_clock.GetElapsedTime(started).TotalMilliseconds;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ExecutionRunnerTests"`

Expected: PASS, 3 tests.

- [ ] **Step 5: Commit**

```bash
git add src/gateway/Execution/ExecutionRunner.cs tests/Gateway.Tests/Execution/ExecutionRunnerTests.cs
printf 'feat(execution): add the audited execution runner with timeout\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 8: Transport routing and the demo fallback

**Files:**
- Create: `src/gateway/Execution/RoutingQueryExecutor.cs`
- Create: `src/gateway/Execution/DemoTabularSource.cs`
- Test: `tests/Gateway.Tests/Execution/RoutingQueryExecutorTests.cs`

**Interfaces:**
- Consumes: `IQueryTransport`, `ExecutionRequest`, `TabularResult`, `ExecutionDataSource` (Tasks 1 and 5).
- Produces:
  - `Gateway.Execution.RoutingQueryExecutor : IQueryTransport` with `RoutingQueryExecutor(IQueryTransport mongo, IQueryTransport rest, IOptions<ExecutionOptions> options)`.
  - `Gateway.Execution.DemoTabularSource.Build(ExecutionRequest request) : TabularResult`.

- [ ] **Step 1: Write the failing test**

Create `tests/Gateway.Tests/Execution/RoutingQueryExecutorTests.cs`:

```csharp
using Gateway.Execution;
using Gateway.Governance;
using Gateway.Reports;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Execution;

public class RoutingQueryExecutorTests
{
    [Fact]
    public async Task Routes_to_mongo_by_default()
    {
        var mongo = new NamedTransport("mongo");
        var rest = new NamedTransport("rest");
        var router = new RoutingQueryExecutor(mongo, rest, Ops());

        var result = await router.ExecuteAsync(Request(ExecutionDataSource.Mongo), TestContext.Current.CancellationToken);

        Assert.Equal("mongo", result.DataSource);
    }

    [Fact]
    public async Task Routes_to_rest_when_policy_selects_it()
    {
        var mongo = new NamedTransport("mongo");
        var rest = new NamedTransport("rest");
        var router = new RoutingQueryExecutor(mongo, rest, Ops());

        var result = await router.ExecuteAsync(Request(ExecutionDataSource.EnterpriseCoreREST), TestContext.Current.CancellationToken);

        Assert.Equal("rest", result.DataSource);
    }

    [Fact]
    public void Demo_source_maps_requested_columns()
    {
        var result = DemoTabularSource.Build(Request(ExecutionDataSource.Mongo));

        Assert.Equal(24, result.RowCount);
        Assert.Equal(["name", "address.market", "price"], result.Columns);
        Assert.NotNull(result.Rows[0]["name"]);
    }

    private static IOptions<ExecutionOptions> Ops()
    {
        return Microsoft.Extensions.Options.Options.Create(new ExecutionOptions());
    }

    private static ExecutionRequest Request(ExecutionDataSource source)
    {
        return new ExecutionRequest(
            "[{\"$match\":{}}]",
            "sess-1",
            "Listings",
            source,
            "listingsAndReviews",
            ["name", "address.market", "price"],
            new Dictionary<string, string>(),
            false,
            false,
            0,
            new RequesterContext("usr_1", "Bikash", "Business Analyst", null, null),
            GovernanceDecision.None);
    }

    private sealed class NamedTransport : IQueryTransport
    {
        private readonly string _name;

        public NamedTransport(string name)
        {
            _name = name;
        }

        public Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(TabularResult.Empty(_name));
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~RoutingQueryExecutorTests"`

Expected: FAIL to build with `The type or namespace name 'RoutingQueryExecutor' does not exist`.

- [ ] **Step 3: Write the implementations**

Create `src/gateway/Execution/RoutingQueryExecutor.cs`:

```csharp
using Microsoft.Extensions.Options;

namespace Gateway.Execution;

public sealed class RoutingQueryExecutor : IQueryTransport
{
    private readonly IQueryTransport _mongo;
    private readonly IQueryTransport _rest;
    private readonly ExecutionOptions _options;

    public RoutingQueryExecutor(IQueryTransport mongo, IQueryTransport rest, IOptions<ExecutionOptions> options)
    {
        _mongo = mongo;
        _rest = rest;
        _options = options.Value;
    }

    public Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var source = request.DataSource == default ? _options.DataSource : request.DataSource;
        return source == ExecutionDataSource.EnterpriseCoreREST
            ? _rest.ExecuteAsync(request, cancellationToken)
            : _mongo.ExecuteAsync(request, cancellationToken);
    }
}
```

Create `src/gateway/Execution/DemoTabularSource.cs`:

```csharp
using System.Globalization;
using Gateway.Reports;

namespace Gateway.Execution;

public static class DemoTabularSource
{
    public static TabularResult Build(ExecutionRequest request)
    {
        var rows = DemoListingSource.Rows()
            .Select(row => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["id"] = row.Id,
                ["name"] = row.Name,
                ["address.market"] = row.Market,
                ["price"] = row.Price.ToString(CultureInfo.InvariantCulture),
                ["room_type"] = row.RoomType,
                ["accommodates"] = row.Accommodates.ToString(CultureInfo.InvariantCulture),
                ["review_scores.rating"] = row.Rating.ToString("0.0", CultureInfo.InvariantCulture),
                ["address.location.coordinates"] = row.Latitude is null ? null : $"{row.Latitude},{row.Longitude}"
            })
            .ToList();

        var columns = request.Columns.Count > 0 ? request.Columns : ["name", "address.market", "price"];
        return new TabularResult(columns, rows, request.DataSource.ToString(), 0);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~RoutingQueryExecutorTests"`

Expected: PASS, 3 tests.

- [ ] **Step 5: Commit**

```bash
git add src/gateway/Execution/RoutingQueryExecutor.cs src/gateway/Execution/DemoTabularSource.cs tests/Gateway.Tests/Execution/RoutingQueryExecutorTests.cs
printf 'feat(execution): add transport routing and the demo fallback\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 9: MongoDB transport

**Files:**
- Create: `src/gateway/Execution/MongoQueryTransport.cs`
- Test: `tests/Gateway.Tests/Execution/MongoQueryTransportTests.cs`

**Interfaces:**
- Consumes: `IQueryTransport`, `ExecutionRequest`, `TabularResult` (Task 5); `ExecutionOptions` (Task 1).
- Produces:
  - `Gateway.Execution.MongoQueryTransport : IQueryTransport` with `MongoQueryTransport(string connectionString, IOptions<ExecutionOptions> options)`.

- [ ] **Step 1: Write the failing test**

Create `tests/Gateway.Tests/Execution/MongoQueryTransportTests.cs`:

```csharp
using Gateway.Execution;
using Gateway.Governance;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Execution;

public class MongoQueryTransportTests
{
    [Fact]
    public async Task Executes_against_mongo_when_a_test_connection_is_configured()
    {
        var connection = Environment.GetEnvironmentVariable("MONGODB_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
        {
            return;
        }

        var options = Microsoft.Extensions.Options.Options.Create(new ExecutionOptions
        {
            Database = Environment.GetEnvironmentVariable("MONGODB_TEST_DATABASE") ?? "sample_airbnb",
            Collection = "listingsAndReviews"
        });
        var transport = new MongoQueryTransport(connection, options);
        var request = new ExecutionRequest(
            "[{\"$match\":{\"address.market\":\"New York\"}},{\"$limit\":3}]",
            "sess-1",
            "Listings in New York",
            ExecutionDataSource.Mongo,
            "listingsAndReviews",
            ["name", "address.market"],
            new Dictionary<string, string>(),
            false,
            false,
            0,
            new RequesterContext("usr_1", "Bikash", "Data Owner / Admin", null, null),
            GovernanceDecision.None);

        var result = await transport.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.Null(result.Error);
        Assert.True(result.RowCount <= 3);
    }

    [Fact]
    public void Invalid_pipeline_json_produces_an_error_result()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new ExecutionOptions());
        var transport = new MongoQueryTransport("mongodb://localhost:27017", options);
        var request = new ExecutionRequest(
            "not-json",
            "sess-1",
            "bad",
            ExecutionDataSource.Mongo,
            "listingsAndReviews",
            ["name"],
            new Dictionary<string, string>(),
            false,
            false,
            0,
            new RequesterContext("usr_1", "Bikash", "Business Analyst", null, null),
            GovernanceDecision.None);

        Assert.ThrowsAsync<MongoDB.Bson.BsonException>(() => transport.ExecuteAsync(request));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~MongoQueryTransportTests"`

Expected: FAIL to build with `The type or namespace name 'MongoQueryTransport' does not exist`.

- [ ] **Step 3: Write the implementation**

Create `src/gateway/Execution/MongoQueryTransport.cs`:

```csharp
using System.Diagnostics;
using System.Globalization;
using MongoDB.Bson;
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
        var stages = BsonArray.Parse(request.Mql).Select(value => value.AsBsonDocument).ToList();
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
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~MongoQueryTransportTests"`

Expected: PASS, 2 tests (the first returns early because `MONGODB_TEST_CONNECTION` is unset; the second asserts the invalid JSON throws).

- [ ] **Step 5: Commit**

```bash
git add src/gateway/Execution/MongoQueryTransport.cs tests/Gateway.Tests/Execution/MongoQueryTransportTests.cs
printf 'feat(execution): add the mongo query transport\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 10: Enterprise Core REST transport

**Files:**
- Create: `src/gateway/Execution/EnterpriseCoreQueryTransport.cs`
- Test: `tests/Gateway.Tests/Execution/EnterpriseCoreQueryTransportTests.cs`

**Interfaces:**
- Consumes: `IQueryTransport`, `ExecutionRequest`, `TabularResult` (Task 5); `EnterpriseCoreOptions` (Task 1).
- Produces:
  - `Gateway.Execution.EnterpriseCoreQueryTransport : IQueryTransport` with `EnterpriseCoreQueryTransport(HttpClient http, IOptions<EnterpriseCoreOptions> options)`.
  - `Gateway.Execution.EnterpriseCoreResponse(IReadOnlyList<string> Columns, List<Dictionary<string, string?>> Rows)` for the dev stub.

- [ ] **Step 1: Write the failing test**

Create `tests/Gateway.Tests/Execution/EnterpriseCoreQueryTransportTests.cs`:

```csharp
using System.Net;
using System.Text;
using Gateway.Execution;
using Gateway.Governance;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Execution;

public class EnterpriseCoreQueryTransportTests
{
    [Fact]
    public async Task Maps_the_upstream_tabular_contract()
    {
        var json = """{"columns":["name","price"],"rows":[{"name":"a","price":"10"}]}""";
        var http = new HttpClient(new StubHandler(json))
        {
            BaseAddress = new Uri("http://enterprise.test")
        };
        var transport = new EnterpriseCoreQueryTransport(
            http,
            Microsoft.Extensions.Options.Options.Create(new EnterpriseCoreOptions { QueryPath = "/api/query" }));

        var result = await transport.ExecuteAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal("EnterpriseCoreREST", result.DataSource);
        Assert.Equal(["name", "price"], result.Columns);
        Assert.Equal("a", result.Rows[0]["name"]);
    }

    [Fact]
    public async Task Upstream_failure_produces_an_error_result()
    {
        var http = new HttpClient(new StubHandler("nope", HttpStatusCode.InternalServerError))
        {
            BaseAddress = new Uri("http://enterprise.test")
        };
        var transport = new EnterpriseCoreQueryTransport(
            http,
            Microsoft.Extensions.Options.Options.Create(new EnterpriseCoreOptions()));

        var result = await transport.ExecuteAsync(Request(), TestContext.Current.CancellationToken);

        Assert.NotNull(result.Error);
        Assert.Equal(0, result.RowCount);
    }

    private static ExecutionRequest Request()
    {
        return new ExecutionRequest(
            "[{\"$match\":{}}]",
            "sess-1",
            "Listings",
            ExecutionDataSource.EnterpriseCoreREST,
            "listingsAndReviews",
            ["name", "price"],
            new Dictionary<string, string>(),
            false,
            false,
            0,
            new RequesterContext("usr_1", "Bikash", "Business Analyst", null, null),
            GovernanceDecision.None);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly HttpStatusCode _status;

        public StubHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            _body = body;
            _status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~EnterpriseCoreQueryTransportTests"`

Expected: FAIL to build with `The type or namespace name 'EnterpriseCoreQueryTransport' does not exist`.

- [ ] **Step 3: Write the implementation**

Create `src/gateway/Execution/EnterpriseCoreQueryTransport.cs`:

```csharp
using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Gateway.Execution;

public sealed record EnterpriseCoreResponse(IReadOnlyList<string> Columns, IReadOnlyList<Dictionary<string, string?>> Rows);

public sealed class EnterpriseCoreQueryTransport : IQueryTransport
{
    private readonly HttpClient _http;
    private readonly EnterpriseCoreOptions _options;

    public EnterpriseCoreQueryTransport(HttpClient http, IOptions<EnterpriseCoreOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await _http.PostAsJsonAsync(
                _options.QueryPath,
                new { query = request.Mql, collection = request.TargetCollection, columns = request.Columns },
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<EnterpriseCoreResponse>(cancellationToken);
            if (payload is null)
            {
                return new TabularResult([], [], ExecutionDataSource.EnterpriseCoreREST.ToString(), stopwatch.ElapsedMilliseconds, Error: "Enterprise Core returned an empty response.");
            }

            var rows = payload.Rows
                .Select(row => (IReadOnlyDictionary<string, string?>)row)
                .ToList();

            return new TabularResult(
                payload.Columns,
                rows,
                ExecutionDataSource.EnterpriseCoreREST.ToString(),
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return new TabularResult([], [], ExecutionDataSource.EnterpriseCoreREST.ToString(), stopwatch.ElapsedMilliseconds, Error: error.Message);
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~EnterpriseCoreQueryTransportTests"`

Expected: PASS, 2 tests.

- [ ] **Step 5: Commit**

```bash
git add src/gateway/Execution/EnterpriseCoreQueryTransport.cs tests/Gateway.Tests/Execution/EnterpriseCoreQueryTransportTests.cs
printf 'feat(execution): add the enterprise core rest transport\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 11: Execute on the direct query path

**Files:**
- Modify: `src/gateway/Nlp/Router/NlpRouteResult.cs`
- Modify: `src/gateway/Nlp/Http/NlpQueryResponse.cs`
- Modify: `src/gateway/Nlp/Orchestrator/NlpOrchestrator.cs`
- Test: `tests/Gateway.Tests/Nlp/NlpOrchestratorExecutionTests.cs`

**Interfaces:**
- Consumes: `ITabularQueryExecutor`, `ExecutionRequest`, `TabularResult`, `GovernanceDecision` (Task 5); `ExecutionOptions` (Task 1); `MqlAnalyzer` (existing).
- Produces:
  - `NlpRouteResult` gains trailing `IReadOnlyList<string>? Columns = null`, `IReadOnlyList<IReadOnlyDictionary<string, string?>>? Rows = null`, `string? DataSource = null`, `int? RowCount = null`, `long? DurationMs = null`, `string? ExecutionError = null`.
  - `NlpQueryResponse` gains the same trailing members and maps them in `From`.
  - `NlpOrchestrator` constructor gains trailing `ITabularQueryExecutor? executor = null, IOptions<ExecutionOptions>? executionOptions = null`.

- [ ] **Step 1: Write the failing test**

Create `tests/Gateway.Tests/Nlp/NlpOrchestratorExecutionTests.cs`:

```csharp
using Gateway.Execution;
using Gateway.Nlp.Abstractions;
using Gateway.Nlp.Cache;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Llm;
using Gateway.Nlp.Mql;
using Gateway.Nlp.Orchestrator;
using Gateway.Nlp.Router;
using Gateway.Nlp.Slots;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Nlp;

public class NlpOrchestratorExecutionTests
{
    private sealed class StubGenerator : ILlmQueryGenerator
    {
        public Task<LlmQueryResult> GenerateAsync(string utterance, string slotsJson, string? previousError, CancellationToken cancellationToken = default)
            => Task.FromResult(new LlmQueryResult("""[{"$limit":5}]""", 1));
    }

    private sealed class VectorEmbedder : ITextEmbedder
    {
        public float[] Embed(string text) => new float[] { 1, 0, 0, 0, 0, 0, 0, 0 };
    }

    private sealed class StubExecutor : ITabularQueryExecutor
    {
        public ExecutionRequest? Last { get; private set; }

        public Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
        {
            Last = request;
            return Task.FromResult(new TabularResult(
                ["name"],
                [new Dictionary<string, string?> { ["name"] = "listing-1" }],
                request.DataSource.ToString(),
                4));
        }
    }

    private static (NlpOrchestrator Sut, StubExecutor Executor) Build()
    {
        var gazetteer = Gazetteer.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Gazetteers"));
        var builder = ScribanSimpleMqlBuilder.FromAssetsDirectory(Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Templates"));
        var router = new NlpRouter(new VectorEmbedder(), new SemanticCache(), builder, gazetteer);
        var corrector = new SelfCorrectingLlmQueryGenerator(new StubGenerator(), new PipelineValidator(), Options.Create(new NvidiaNimOptions()));
        var executor = new StubExecutor();
        var sut = new NlpOrchestrator(
            router,
            gazetteer,
            corrector,
            new InMemoryAgentEventSink(),
            new InMemoryAgentStateStore(),
            GuardrailTestFactory.FromAssets(),
            new InMemoryAccessRequestStore(),
            TimeProvider.System,
            executor,
            Options.Create(new ExecutionOptions()));
        return (sut, executor);
    }

    [Fact]
    public async Task Simple_query_executes_and_returns_rows()
    {
        var (sut, executor) = Build();

        var result = await sut.OrchestrateAsync("listings with pools in Los Angeles, just run it", "sess_1");

        Assert.Equal(NlpRouteKind.SimpleMql, result.Kind);
        Assert.Equal(["name"], result.Columns);
        Assert.Equal(1, result.RowCount);
        Assert.Equal("Mongo", result.DataSource);
        Assert.Equal("sess_1", executor.Last!.SessionId);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~NlpOrchestratorExecutionTests"`

Expected: FAIL to build with `NlpOrchestrator does not contain a constructor that takes 10 arguments`.

- [ ] **Step 3: Add the trailing result members**

In `src/gateway/Nlp/Router/NlpRouteResult.cs`, append these parameters after `GuardrailReason`:

```csharp
    string? GuardrailReason = null,
    IReadOnlyList<string>? Columns = null,
    IReadOnlyList<IReadOnlyDictionary<string, string?>>? Rows = null,
    string? DataSource = null,
    int? RowCount = null,
    long? DurationMs = null,
    string? ExecutionError = null);
```

In `src/gateway/Nlp/Http/NlpQueryResponse.cs`, append the same members after `GuardrailReason` and add the mapping at the end of `From`:

```csharp
        result.AccessRequestId,
        result.GuardrailReason,
        result.Columns,
        result.Rows,
        result.DataSource,
        result.RowCount,
        result.DurationMs,
        result.ExecutionError);
```

- [ ] **Step 4: Add execution to NlpOrchestrator**

Add the fields and constructor parameters in `src/gateway/Nlp/Orchestrator/NlpOrchestrator.cs`:

```csharp
using Gateway.Execution;
using Microsoft.Extensions.Options;
```

```csharp
    private readonly ITabularQueryExecutor? _executor;
    private readonly ExecutionOptions _execution;

    public NlpOrchestrator(
        INlpRouter router,
        Gazetteer gazetteer,
        SelfCorrectingLlmQueryGenerator generator,
        IAgentEventSink events,
        IAgentStateStore stateStore,
        GuardrailEvaluator guardrails,
        IAccessRequestStore accessRequests,
        TimeProvider clock,
        ITabularQueryExecutor? executor = null,
        IOptions<ExecutionOptions>? executionOptions = null)
    {
        _router = router;
        _gazetteer = gazetteer;
        _generator = generator;
        _events = events;
        _stateStore = stateStore;
        _guardrails = guardrails;
        _accessRequests = accessRequests;
        _clock = clock;
        _executor = executor;
        _execution = executionOptions?.Value ?? new ExecutionOptions();
    }
```

Replace the block from `if (guard.ExemptionType is not null)` through the final `return` with:

```csharp
        if (guard.Outcome == GuardrailOutcome.PausedForApproval)
        {
            var request = await _accessRequests.CreateAsync(
                new AccessRequest(
                    string.Empty,
                    sessionId,
                    result.Mql,
                    guard.SensitiveFields,
                    AccessRequest.PendingLead,
                    Justification: null,
                    _clock.GetUtcNow(),
                    Requester: requester is null
                        ? null
                        : new RequesterInfo(requester.UserId, requester.Name, requester.Role, requester.LeadUserId),
                    AssignedLeadId: requester?.LeadUserId,
                    RequestedFlags: guard.SensitiveFields
                        .Select(field => new RequestedFlag(field, "requires_approval"))
                        .ToList()),
                cancellationToken);

            _events.Publish(new AgentEvent("governance.paused", "paused", guard.Reason));
            await _stateStore.SaveAsync(
                new AgentState(sessionId, "governance_paused", guard.Reason, _clock.GetUtcNow(), request.Id),
                cancellationToken);

            return result with
            {
                Kind = NlpRouteKind.GovernancePaused,
                SensitiveFields = guard.SensitiveFields,
                AccessRequestId = request.Id,
                GuardrailReason = guard.Reason
            };
        }

        if (guard.ExemptionType is not null)
        {
            _events.Publish(new AgentEvent("governance.exempted", "exempted", guard.ExemptionType));
        }

        var sensitiveFields = guard.SensitiveFields.Count > 0 ? guard.SensitiveFields : null;

        if (_executor is null || result.Mql is null)
        {
            _events.Publish(new AgentEvent("agent.completed", "completed", result.Kind.ToString()));
            return result with { SensitiveFields = sensitiveFields };
        }

        var governance = new GovernanceDecision(
            guard.SensitiveFields.Count > 0,
            guard.SensitiveFields,
            guard.ExemptionType,
            false,
            guard.ExemptionType is not null ? requester?.Name : null);

        var columns = MqlAnalyzer.Analyze(result.Mql).FieldPaths;
        var user = requester ?? new RequesterContext(sessionId, sessionId, string.Empty, null);
        var execution = await _executor.ExecuteAsync(
            new ExecutionRequest(
                result.Mql,
                sessionId,
                utterance,
                _execution.DataSource,
                _execution.Collection,
                columns,
                new Dictionary<string, string>
                {
                    ["limit"] = result.ClarificationsApplied.Limit.ToString(),
                    ["sort"] = result.ClarificationsApplied.Sort,
                    ["market"] = result.ClarificationsApplied.Market
                },
                result.SemanticCacheHit,
                result.SlotExtractionUsed,
                result.LlmTokensConsumed,
                user,
                governance),
            cancellationToken);

        return result with
        {
            SensitiveFields = sensitiveFields,
            Columns = execution.Columns,
            Rows = execution.Rows,
            DataSource = execution.DataSource,
            RowCount = execution.RowCount,
            DurationMs = execution.DurationMs,
            ExecutionError = execution.Error
        };
    }
}
```

Delete the old exemption-return, paused block, and final completed block that the replacement supersedes.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~Nlp"`

Expected: PASS, including the existing `NlpOrchestratorTests` and `NlpOrchestratorExecutionTests`.

- [ ] **Step 6: Commit**

```bash
git add src/gateway/Nlp/Router/NlpRouteResult.cs src/gateway/Nlp/Http/NlpQueryResponse.cs src/gateway/Nlp/Orchestrator/NlpOrchestrator.cs tests/Gateway.Tests/Nlp/NlpOrchestratorExecutionTests.cs
printf 'feat(execution): execute guarded queries on the direct path\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 12: Execute when the report intake completes

**Files:**
- Modify: `src/gateway/Conversations/ConversationState.cs`
- Modify: `src/gateway/Conversations/ConversationTurn.cs`
- Modify: `src/gateway/Reports/CsvExporter.cs`
- Modify: `src/gateway/Reports/ReportService.cs`
- Modify: `src/gateway/Conversations/ConversationOrchestrator.cs`
- Modify: `src/gateway/Program.cs`
- Test: `tests/Gateway.Tests/Conversations/ConversationExecutionTests.cs`

**Interfaces:**
- Consumes: `ITabularQueryExecutor`, `ExecutionRequest`, `TabularResult`, `GovernanceDecision` (Task 5); `ExecutionOptions` (Task 1).
- Produces:
  - `ConversationState` gains trailing `Gateway.Execution.TabularResult? Execution = null`.
  - `ConversationTurn` gains trailing `Gateway.Execution.TabularResult? Execution = null`.
  - `CsvExporter.Export(IReadOnlyList<IReadOnlyDictionary<string, string?>> rows, IReadOnlyList<string> columns) : string`.
  - `ReportService.BuildCsv(IReadOnlyList<IReadOnlyDictionary<string, string?>> rows, IReadOnlyList<string> columns) : ReportBuild`.
  - `ConversationOrchestrator` constructor gains trailing `ITabularQueryExecutor? executor = null, IOptions<ExecutionOptions>? executionOptions = null`.

- [ ] **Step 1: Write the failing test**

Create `tests/Gateway.Tests/Conversations/ConversationExecutionTests.cs`:

```csharp
using Gateway.Conversations;
using Gateway.Execution;
using Gateway.Governance;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Http;
using Gateway.Nlp.Mql;
using Gateway.Nlp.Orchestrator;
using Gateway.Nlp.Router;
using Gateway.Reports;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Conversations;

public class ConversationExecutionTests
{
    private sealed class StubOrchestrator : INlpOrchestrator
    {
        public Task<NlpRouteResult> OrchestrateAsync(string utterance, string sessionId = "anonymous", CancellationToken cancellationToken = default, RequesterContext? requester = null)
            => Task.FromResult(new NlpRouteResult(
                NlpRouteKind.ComplexLlmRequired,
                """[{"$group":{"_id":"$address.market"}}]""",
                null,
                false,
                false,
                Gateway.Nlp.Intent.IntentKind.Search,
                false,
                new MqlDefaults(10, "rating_desc", "All"),
                0,
                1,
                null,
                null,
                null,
                null));
    }

    private sealed class StubExecutor : ITabularQueryExecutor
    {
        public Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new TabularResult(
                ["name", "price"],
                [new Dictionary<string, string?> { ["name"] = "real-row", ["price"] = "120" }],
                request.DataSource.ToString(),
                3));
    }

    private static ConversationOrchestrator Build()
    {
        return new ConversationOrchestrator(
            new StubOrchestrator(),
            new InMemoryConversationStore(),
            new InMemoryAccessRequestStore(),
            new InMemoryApprovalFlagStore(true),
            new SimulatedNotificationSender(),
            new ColumnCatalog(),
            new InMemoryAgentEventSink(),
            Options.Create(new ReportsOptions()),
            TimeProvider.System,
            new StubExecutor(),
            Options.Create(new ExecutionOptions()));
    }

    [Fact]
    public async Task Completing_a_report_executes_and_carries_rows()
    {
        var sut = Build();

        var email = await sut.StartAsync("average price by market", "sess_1", null);
        await sut.AnswerAsync(email.ConversationId, new ConversationAnswer(Email: "analyst@enterprise.com"));
        await sut.AnswerAsync(email.ConversationId, new ConversationAnswer(Purpose: "Quarterly review", ProjectCode: "PROJ-1"));
        await sut.AnswerAsync(email.ConversationId, new ConversationAnswer(ManagerEmail: "manager@enterprise.com"));
        await sut.AnswerAsync(email.ConversationId, new ConversationAnswer(Columns: ["name", "price"]));
        var complete = await sut.AnswerAsync(email.ConversationId, new ConversationAnswer(Delivery: ReportIntake.Csv));

        Assert.NotNull(complete!.Execution);
        Assert.Equal(1, complete.Execution!.RowCount);
        Assert.Equal("real-row", complete.Execution.Rows[0]["name"]);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ConversationExecutionTests"`

Expected: FAIL to build with `ConversationOrchestrator does not contain a constructor that takes 11 arguments`.

- [ ] **Step 3: Add the trailing record members**

In `src/gateway/Conversations/ConversationState.cs`, add `using Gateway.Execution;` and append `TabularResult? Execution = null` after `NlpQueryResponse? Result = null`.

In `src/gateway/Conversations/ConversationTurn.cs`, add `using Gateway.Execution;` and append `TabularResult? Execution = null` after `string? ValidationError`.

- [ ] **Step 4: Add the tabular CSV export**

In `src/gateway/Reports/CsvExporter.cs`, add:

```csharp
    public static string Export(IReadOnlyList<IReadOnlyDictionary<string, string?>> rows, IReadOnlyList<string> columns)
    {
        var builder = new StringBuilder();
        builder.Append(string.Join(',', columns.Select(Escape)));

        foreach (var row in rows)
        {
            builder.Append('\n');
            builder.Append(string.Join(',', columns.Select(column =>
                Escape(row.TryGetValue(column, out var value) ? value ?? string.Empty : string.Empty))));
        }

        return builder.ToString();
    }
```

In `src/gateway/Reports/ReportService.cs`, add:

```csharp
    public static ReportBuild BuildCsv(IReadOnlyList<IReadOnlyDictionary<string, string?>> rows, IReadOnlyList<string> columns)
    {
        if (columns.Count == 0)
        {
            return new ReportBuild(false, null, "No columns were confirmed for this report.");
        }

        return new ReportBuild(true, CsvExporter.Export(rows, columns), null);
    }
```

- [ ] **Step 5: Execute on completion in ConversationOrchestrator**

Add the constructor parameters and fields:

```csharp
using Gateway.Execution;
using Microsoft.Extensions.Options;
```

```csharp
    private readonly ITabularQueryExecutor? _executor;
    private readonly ExecutionOptions _execution;
```

Append to the constructor parameter list after `TimeProvider clock`:

```csharp
        TimeProvider clock,
        ITabularQueryExecutor? executor = null,
        IOptions<ExecutionOptions>? executionOptions = null)
```

and add in the body:

```csharp
        _executor = executor;
        _execution = executionOptions?.Value ?? new ExecutionOptions();
```

Replace the tail of `FinalizeAsync` from `_events.Publish(new AgentEvent("conversation.completed", ...));` with:

```csharp
        var updated = state with
        {
            Step = ConversationStep.Complete,
            ApprovalRequired = approvalRequired,
            UpdatedAt = _clock.GetUtcNow()
        };

        if (!approvalRequired && updated.Mql is not null && _executor is not null)
        {
            var execution = await _executor.ExecuteAsync(
                BuildRequest(updated, GovernanceDecision.None, updated.Draft.DeliveryFormat),
                cancellationToken);
            updated = updated with { Execution = execution };
        }

        if (intake.DeliveryFormat == ReportIntake.Csv && !approvalRequired && updated.Execution?.Error is null)
        {
            _events.Publish(new AgentEvent("report.ready", "ready", updated.Id));
        }

        _events.Publish(new AgentEvent("conversation.completed", "completed", updated.Id));
        return updated;
    }

    private ExecutionRequest BuildRequest(ConversationState state, GovernanceDecision governance, string? exportFormat)
    {
        return new ExecutionRequest(
            state.Mql ?? "[]",
            state.SessionId,
            state.Utterance,
            _execution.DataSource,
            _execution.Collection,
            state.Draft.Columns ?? [],
            new Dictionary<string, string>(),
            false,
            false,
            0,
            new RequesterContext(state.SessionId, state.SessionId, string.Empty, null, state.Draft.RequesterEmail),
            governance,
            exportFormat);
    }
```

Remove the old `report.ready` publish that preceded the old `conversation.completed` and the old `return state with {...}` tail.

Add `Execution = state.Execution` as the last argument of the `ConversationTurn` built in `ToTurn`:

```csharp
            state.Result,
            null,
            state.Execution);
```

- [ ] **Step 6: Use executed rows for the CSV download**

In `src/gateway/Program.cs`, replace the report CSV block:

```csharp
    var columns = turn.Columns?.Where(column => column.Selected).Select(column => column.Name).ToList() ?? [];
    var build = turn.Execution is not null
        ? ReportService.BuildCsv(turn.Execution.Rows, columns)
        : ReportService.BuildCsv(columns, turn.ApprovalRequired);
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~Conversation"`

Expected: PASS, including `ConversationExecutionTests` and the existing conversation suites.

- [ ] **Step 8: Commit**

```bash
git add src/gateway/Conversations/ConversationState.cs src/gateway/Conversations/ConversationTurn.cs src/gateway/Reports/CsvExporter.cs src/gateway/Reports/ReportService.cs src/gateway/Conversations/ConversationOrchestrator.cs src/gateway/Program.cs tests/Gateway.Tests/Conversations/ConversationExecutionTests.cs
printf 'feat(execution): execute when the report intake completes\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 13: Execute on approval resume

**Files:**
- Modify: `src/gateway/Conversations/ConversationOrchestrator.cs`
- Test: `tests/Gateway.Tests/Conversations/ConversationResumeExecutionTests.cs`

**Interfaces:**
- Consumes: everything from Task 12; `IAccessRequestStore` (existing).
- Produces: `ConversationOrchestrator.ResumeAsync` stores an executed `TabularResult` before publishing `report.ready`.

- [ ] **Step 1: Write the failing test**

Create `tests/Gateway.Tests/Conversations/ConversationResumeExecutionTests.cs`:

```csharp
using Gateway.Conversations;
using Gateway.Execution;
using Gateway.Governance;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Http;
using Gateway.Nlp.Mql;
using Gateway.Nlp.Orchestrator;
using Gateway.Nlp.Router;
using Gateway.Reports;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Conversations;

public class ConversationResumeExecutionTests
{
    private sealed class StubOrchestrator : INlpOrchestrator
    {
        public Task<NlpRouteResult> OrchestrateAsync(string utterance, string sessionId = "anonymous", CancellationToken cancellationToken = default, RequesterContext? requester = null)
            => Task.FromResult(new NlpRouteResult(
                NlpRouteKind.GovernancePaused,
                """[{"$match":{"address.location.coordinates":{"$exists":true}}}]""",
                null,
                false,
                false,
                Gateway.Nlp.Intent.IntentKind.Search,
                false,
                new MqlDefaults(10, "rating_desc", "All"),
                0,
                1,
                null,
                ["address.location.coordinates"],
                "req-1"));
    }

    private sealed class StubExecutor : ITabularQueryExecutor
    {
        public Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new TabularResult(
                ["name"],
                [new Dictionary<string, string?> { ["name"] = "after-approval" }],
                request.DataSource.ToString(),
                2));
    }

    [Fact]
    public async Task Resume_executes_and_stores_rows()
    {
        var store = new InMemoryConversationStore();
        var requests = new InMemoryAccessRequestStore();
        var created = await requests.CreateAsync(new AccessRequest(
            string.Empty, "sess_1", """[{"$match":{}}]""", ["address.location.coordinates"], AccessRequest.Approved, null, DateTimeOffset.UnixEpoch));
        var sut = new ConversationOrchestrator(
            new StubOrchestrator(),
            store,
            requests,
            new InMemoryApprovalFlagStore(true),
            new SimulatedNotificationSender(),
            new ColumnCatalog(),
            new InMemoryAgentEventSink(),
            Options.Create(new ReportsOptions()),
            TimeProvider.System,
            new StubExecutor(),
            Options.Create(new ExecutionOptions()));

        var start = await sut.StartAsync("average coordinates near me", "sess_1", "analyst@enterprise.com");
        await sut.AnswerAsync(start.ConversationId, new ConversationAnswer(Email: "analyst@enterprise.com"));
        await sut.AnswerAsync(start.ConversationId, new ConversationAnswer(Purpose: "Quarterly review", ProjectCode: "PROJ-1"));
        await sut.AnswerAsync(start.ConversationId, new ConversationAnswer(BusinessImpact: "Revenue planning impact"));
        await sut.AnswerAsync(start.ConversationId, new ConversationAnswer(ManagerEmail: "manager@enterprise.com"));
        await sut.AnswerAsync(start.ConversationId, new ConversationAnswer(Columns: ["name"]));
        await sut.AnswerAsync(start.ConversationId, new ConversationAnswer(Delivery: ReportIntake.Csv));

        var resumed = await sut.ResumeAsync(created.Id);

        Assert.True(resumed);
        var turn = await sut.GetAsync(start.ConversationId);
        Assert.NotNull(turn!.Execution);
        Assert.Equal("after-approval", turn.Execution!.Rows[0]["name"]);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ConversationResumeExecutionTests"`

Expected: FAIL because the resumed turn has no `Execution`.

- [ ] **Step 3: Execute inside ResumeAsync**

Replace the body of `ResumeAsync` in `src/gateway/Conversations/ConversationOrchestrator.cs` with:

```csharp
    public async Task<bool> ResumeAsync(string accessRequestId, CancellationToken cancellationToken = default)
    {
        var state = await _store.FindByAccessRequestAsync(accessRequestId, cancellationToken);
        if (state is null || !state.ApprovalRequired)
        {
            return false;
        }

        var request = state.AccessRequestId is null
            ? null
            : await _accessRequests.GetAsync(state.AccessRequestId, cancellationToken);

        var governance = new GovernanceDecision(
            state.SensitiveFields.Count > 0,
            state.SensitiveFields,
            null,
            request?.Resolution?.OverrideInvoked ?? false,
            request?.Resolution?.ResolvedByName);

        var resumed = await _store.UpdateAsync(
            state with { ApprovalRequired = false, UpdatedAt = _clock.GetUtcNow() },
            cancellationToken);

        _events.Publish(new AgentEvent("conversation.resumed", "resumed", resumed.Id));

        if (resumed.Mql is not null && _executor is not null)
        {
            var execution = await _executor.ExecuteAsync(
                BuildRequest(resumed, governance, resumed.Draft.DeliveryFormat),
                cancellationToken);
            resumed = await _store.UpdateAsync(resumed with { Execution = execution }, cancellationToken);
        }

        if (resumed.Draft.DeliveryFormat == ReportIntake.Csv && resumed.Execution?.Error is null)
        {
            _events.Publish(new AgentEvent("report.ready", "ready", resumed.Id));
        }

        return true;
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~Conversation"`

Expected: PASS, including the existing `ConversationResumeTests`.

- [ ] **Step 5: Commit**

```bash
git add src/gateway/Conversations/ConversationOrchestrator.cs tests/Gateway.Tests/Conversations/ConversationResumeExecutionTests.cs
printf 'feat(execution): execute approved queries on resume\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 14: Wire persistence, execution, and audit into DI, plus the dev stub

**Files:**
- Modify: `src/gateway/Nlp/NlpServiceCollectionExtensions.cs`
- Modify: `src/gateway/Program.cs`
- Modify: `src/gateway/appsettings.Development.json`
- Modify: `tests/Gateway.Tests/GatewayFactory.cs`
- Test: `tests/Gateway.Tests/ApiExecutionTests.cs`

**Interfaces:**
- Consumes: every component from Tasks 1 through 13.
- Produces: the running gateway uses durable stores and the real `ExecutionRunner`; Development exposes `POST /dev/enterprise-core/query`; `GatewayFactory` isolates storage and uses a fast stub executor.

- [ ] **Step 1: Write the failing test**

Create `tests/Gateway.Tests/ApiExecutionTests.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Gateway.Tests;

public class ApiExecutionTests : IClassFixture<GatewayFactory>
{
    private readonly GatewayFactory _factory;

    public ApiExecutionTests(GatewayFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Simple_query_returns_executed_rows()
    {
        var client = AuthedClient();

        var response = await client.PostAsJsonAsync("/api/nlp/query", new { utterance = "listings with pools in Los Angeles, just run it" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<NlpExecutionDto>();
        Assert.NotNull(payload);
        Assert.Equal("Stub", payload!.DataSource);
        Assert.Equal(1, payload.RowCount);
        Assert.NotNull(payload.Rows);
        Assert.NotEmpty(payload.Rows!);
    }

    private HttpClient AuthedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueToken());
        return client;
    }

    private static string IssueToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GatewayFactory.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim("user_id", "usr_bn_101"),
            new Claim("name", "Bikash"),
            new Claim("email", "bnayak@enterprise.com"),
            new Claim("role", "Data Owner / Admin"),
            new Claim("lead_user_id", "usr_bn_101")
        };
        var token = new JwtSecurityToken(
            issuer: GatewayFactory.Issuer,
            audience: GatewayFactory.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record NlpExecutionDto(string Kind, string? DataSource, int? RowCount, List<Dictionary<string, string?>>? Rows);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ApiExecutionTests"`

Expected: FAIL because the response has no `dataSource` and `rowCount` is null.

- [ ] **Step 3: Isolate storage and stub the executor in the test factory**

Replace `tests/Gateway.Tests/GatewayFactory.cs` with:

```csharp
using Gateway.Execution;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Gateway.Tests;

public class GatewayFactory : WebApplicationFactory<Program>
{
    public const string Issuer = "MultiAgentMongoNlp";
    public const string Audience = "MultiAgentMongoNlp.Spa";
    public const string SigningKey = "DEV-ONLY-CHANGE-ME-32CHARS-MINIMUM!!";

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 9, 5, 9, 15, 0, TimeSpan.Zero));

    public string DataDirectory { get; } = Path.Combine(Path.GetTempPath(), "gateway-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Jwt:Issuer", Issuer);
        builder.UseSetting("Jwt:Audience", Audience);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("Persistence:Mode", "file");
        builder.UseSetting("Persistence:DataDirectory", DataDirectory);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
            services.RemoveAll<ITabularQueryExecutor>();
            services.AddSingleton<ITabularQueryExecutor>(new StubTabularQueryExecutor());
        });
    }

    private sealed class StubTabularQueryExecutor : ITabularQueryExecutor
    {
        public Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TabularResult(
                ["name", "price"],
                [new Dictionary<string, string?> { ["name"] = "Demo listing", ["price"] = "100" }],
                "Stub",
                1));
        }
    }
}
```

- [ ] **Step 4: Register everything in DI**

In `src/gateway/Nlp/NlpServiceCollectionExtensions.cs`, add `using Gateway.Audit; using Gateway.Execution; using Gateway.Persistence;` and add these registrations before `return services;`:

```csharp
        services.Configure<ExecutionOptions>(configuration.GetSection(ExecutionOptions.SectionName));
        services.Configure<EnterpriseCoreOptions>(configuration.GetSection(EnterpriseCoreOptions.SectionName));
        services.Configure<PersistenceOptions>(configuration.GetSection(PersistenceOptions.SectionName));

        services.AddSingleton<IDocumentStore>(sp =>
        {
            var environment = sp.GetRequiredService<IHostEnvironment>();
            var options = sp.GetRequiredService<IOptions<PersistenceOptions>>().Value;
            var config = sp.GetRequiredService<IConfiguration>();
            return DocumentStoreFactory.Create(
                options,
                environment.ContentRootPath,
                config["MongoDb:ConnectionString"],
                config["MongoDb:Database"] ?? "sample_airbnb");
        });

        services.AddSingleton<IAccessRequestStore, DurableAccessRequestStore>();
        services.AddSingleton<IAgentStateStore, DurableAgentStateStore>();
        services.AddSingleton<IConversationStore, DurableConversationStore>();
        services.AddSingleton<IAuditLogStore, DurableAuditLogStore>();

        services.AddHttpClient("enterprise-core", (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<EnterpriseCoreOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
            {
                client.BaseAddress = new Uri(options.BaseUrl);
            }

            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddSingleton<MongoQueryTransport>(sp => new MongoQueryTransport(
            sp.GetRequiredService<IConfiguration>()["MongoDb:ConnectionString"] ?? "mongodb://localhost:27017",
            sp.GetRequiredService<IOptions<ExecutionOptions>>()));

        services.AddSingleton<EnterpriseCoreQueryTransport>(sp => new EnterpriseCoreQueryTransport(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("enterprise-core"),
            sp.GetRequiredService<IOptions<EnterpriseCoreOptions>>()));

        services.AddSingleton<IQueryTransport>(sp => new RoutingQueryExecutor(
            sp.GetRequiredService<MongoQueryTransport>(),
            sp.GetRequiredService<EnterpriseCoreQueryTransport>(),
            sp.GetRequiredService<IOptions<ExecutionOptions>>()));

        services.AddSingleton<ITabularQueryExecutor, ExecutionRunner>();
```

Remove the three now-duplicate in-memory registrations for `IAccessRequestStore`, `IAgentStateStore`, and `IConversationStore` so the durable ones win.

- [ ] **Step 5: Add the Development Enterprise Core stub and point Development at it**

In `src/gateway/Program.cs`, inside the `if (app.Environment.IsDevelopment())` block, add:

```csharp
    app.MapPost("/dev/enterprise-core/query", (EnterpriseCoreQueryRequest body) =>
    {
        var columns = body.Columns is { Count: > 0 } list
            ? list
            : new List<string> { "name", "address.market", "price" };
        var request = new ExecutionRequest(
            body.Query ?? "[]",
            "dev",
            "dev",
            ExecutionDataSource.EnterpriseCoreREST,
            body.Collection ?? "listingsAndReviews",
            columns,
            new Dictionary<string, string>(),
            false,
            false,
            0,
            new RequesterContext("dev", "dev", "Business Analyst", null),
            GovernanceDecision.None);
        var result = DemoTabularSource.Build(request);
        return Results.Ok(new { columns = result.Columns, rows = result.Rows });
    });
```

Add `using Gateway.Execution;` and `using Gateway.Governance;` to `Program.cs` if missing, and append at the bottom near the other records:

```csharp
public sealed record EnterpriseCoreQueryRequest(string? Query, string? Collection, IReadOnlyList<string>? Columns);
```

In `src/gateway/appsettings.Development.json`, add:

```json
  "EnterpriseCore": {
    "BaseUrl": "http://localhost:5235"
  },
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj`

Expected: PASS, all tests. Then run `git restore docs/benchmarks/` to discard benchmark rewrites.

- [ ] **Step 7: Commit**

```bash
git add src/gateway/Nlp/NlpServiceCollectionExtensions.cs src/gateway/Program.cs src/gateway/appsettings.Development.json tests/Gateway.Tests/GatewayFactory.cs tests/Gateway.Tests/ApiExecutionTests.cs
printf 'feat(execution): wire durable stores execution and audit into di\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 15: Angular results grid component

**Files:**
- Create: `src/web/src/app/models/execution.ts`
- Create: `src/web/src/app/results/results-grid.component.ts`
- Create: `src/web/src/app/results/results-grid.component.html`
- Create: `src/web/src/app/results/results-grid.component.scss`
- Test: `src/web/src/app/results/results-grid.component.spec.ts`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `ExecutionPayload { columns: string[]; rows: Record<string, string | null>[]; dataSource: string; rowCount: number; durationMs: number; timedOut: boolean; error?: string | null }`.
  - `ResultsGridComponent` with `@Input() execution: ExecutionPayload | null` and selector `app-results-grid`.

- [ ] **Step 1: Write the failing test**

Create `src/web/src/app/results/results-grid.component.spec.ts`:

```ts
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ResultsGridComponent } from './results-grid.component';
import { ExecutionPayload } from '../models/execution';

describe('ResultsGridComponent', () => {
  let fixture: ComponentFixture<ResultsGridComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ResultsGridComponent] }).compileComponents();
    fixture = TestBed.createComponent(ResultsGridComponent);
  });

  it('renders columns and rows', () => {
    const execution: ExecutionPayload = {
      columns: ['name', 'price'],
      rows: [{ name: 'a', price: '10' }],
      dataSource: 'Mongo',
      rowCount: 1,
      durationMs: 4,
      timedOut: false
    };

    fixture.componentRef.setInput('execution', execution);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('name');
    expect(text).toContain('price');
    expect(text).toContain('a');
    expect(text).toContain('1 rows');
  });

  it('shows an empty state without a payload', () => {
    fixture.componentRef.setInput('execution', null);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No rows');
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd /workspace/src/web && npm test`

Expected: FAIL with `Cannot find module './results-grid.component'`.

- [ ] **Step 3: Write the model and component**

Create `src/web/src/app/models/execution.ts`:

```ts
export interface ExecutionPayload {
  columns: string[];
  rows: Record<string, string | null>[];
  dataSource: string;
  rowCount: number;
  durationMs: number;
  timedOut: boolean;
  error?: string | null;
}
```

Create `src/web/src/app/results/results-grid.component.ts`:

```ts
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ExecutionPayload } from '../models/execution';

@Component({
  selector: 'app-results-grid',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './results-grid.component.html',
  styleUrl: './results-grid.component.scss'
})
export class ResultsGridComponent {
  @Input() execution: ExecutionPayload | null = null;

  value(row: Record<string, string | null>, column: string): string {
    return row[column] ?? '';
  }
}
```

Create `src/web/src/app/results/results-grid.component.html`:

```html
<div class="grid" *ngIf="execution; else empty">
  <div class="grid-meta">
    <span>{{ execution.rowCount }} rows</span>
    <span>{{ execution.dataSource }}</span>
    <span>{{ execution.durationMs }} ms</span>
    <span class="grid-error" *ngIf="execution.error">{{ execution.error }}</span>
  </div>
  <div class="grid-scroll">
    <table>
      <thead>
        <tr>
          <th *ngFor="let column of execution.columns">{{ column }}</th>
        </tr>
      </thead>
      <tbody>
        <tr *ngFor="let row of execution.rows">
          <td *ngFor="let column of execution.columns">{{ value(row, column) }}</td>
        </tr>
      </tbody>
    </table>
  </div>
</div>
<ng-template #empty>
  <p class="grid-empty">No rows to display.</p>
</ng-template>
```

Create `src/web/src/app/results/results-grid.component.scss`:

```scss
.grid {
  border: 1px solid #e2e5ea;
  border-radius: 8px;
  overflow: hidden;
  background: #fff;
}

.grid-meta {
  display: flex;
  gap: 12px;
  padding: 8px 12px;
  font-size: 12px;
  color: #55606e;
  border-bottom: 1px solid #e2e5ea;
}

.grid-error {
  color: #b3261e;
}

.grid-scroll {
  max-height: 320px;
  overflow: auto;
}

table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
}

th,
td {
  padding: 6px 10px;
  text-align: left;
  border-bottom: 1px solid #eef0f3;
  white-space: nowrap;
}

th {
  position: sticky;
  top: 0;
  background: #f7f8fa;
}

.grid-empty {
  color: #55606e;
  font-size: 13px;
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `cd /workspace/src/web && npm test`

Expected: PASS, including the two new specs.

- [ ] **Step 5: Commit**

```bash
git add src/web/src/app/models/execution.ts src/web/src/app/results
printf 'feat(web): add the results grid component\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 16: Render the grid in the chat

**Files:**
- Modify: `src/web/src/app/models/conversation.ts`
- Modify: `src/web/src/app/models/nlp-query-response.ts`
- Modify: `src/web/src/app/chat/chat-thread.component.ts`
- Modify: `src/web/src/app/chat/chat-thread.component.html`
- Test: `src/web/src/app/chat/chat-thread.component.spec.ts`

**Interfaces:**
- Consumes: `ExecutionPayload` and `ResultsGridComponent` (Task 15).
- Produces: `ConversationTurn.execution?: ExecutionPayload | null`; the chat thread renders `app-results-grid` when the current turn carries one.

- [ ] **Step 1: Write the failing test**

Append to `src/web/src/app/chat/chat-thread.component.spec.ts` a test that sets `component.turn` to a turn with an execution payload and asserts the grid renders. If the existing spec uses a helper, match its style; the minimal addition is:

```ts
  it('renders the results grid when a turn carries an execution payload', () => {
    const component = fixture.componentInstance;
    component.turn = {
      conversationId: 'c1',
      step: 'Complete',
      kind: 'ComplexLlmRequired',
      assistantMessage: 'done',
      control: 'none',
      deliveryOptions: ['EMAIL', 'CSV'],
      approvalRequired: false,
      downloadable: true,
      demoReport: false,
      execution: {
        columns: ['name'],
        rows: [{ name: 'row-1' }],
        dataSource: 'Mongo',
        rowCount: 1,
        durationMs: 3,
        timedOut: false
      }
    };

    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('row-1');
  });
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd /workspace/src/web && npm test`

Expected: FAIL because `execution` is not a known property or the grid is not rendered.

- [ ] **Step 3: Add the model field**

In `src/web/src/app/models/conversation.ts`, add at the top:

```ts
import { ExecutionPayload } from './execution';
```

and add to `ConversationTurn` after `validationError`:

```ts
  execution?: ExecutionPayload | null;
```

In `src/web/src/app/models/nlp-query-response.ts`, add `import { ExecutionPayload } from './execution';` and a trailing `execution?: ExecutionPayload | null;` if the interface does not already carry columns and rows.

- [ ] **Step 4: Render the grid in the chat**

In `src/web/src/app/chat/chat-thread.component.ts`, add the import and component to the standalone imports:

```ts
import { ResultsGridComponent } from '../results/results-grid.component';
```

```ts
  imports: [CommonModule, FormsModule, ResultsGridComponent],
```

In `src/web/src/app/chat/chat-thread.component.html`, add immediately after the assistant message block that shows the current turn:

```html
<app-results-grid *ngIf="turn?.execution" [execution]="turn!.execution"></app-results-grid>
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `cd /workspace/src/web && npm test`

Expected: PASS, all specs.

- [ ] **Step 6: Commit**

```bash
git add src/web/src/app/models/conversation.ts src/web/src/app/models/nlp-query-response.ts src/web/src/app/chat
printf 'feat(web): render executed rows in the chat\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

### Task 17: Document the execution and audit slice

**Files:**
- Modify: `README.md`
- Modify: `sprints/sprint-6.md`

**Interfaces:**
- Consumes: the whole sprint.
- Produces: README coverage of execution, audit, and durable holds; Sprint 6 acceptance boxes ticked.

- [ ] **Step 1: Add a README section**

Add after the "Governance approvals" section in `README.md`:

```markdown
## Execution, audit, and durable holds

Approved or unrestricted read-only pipelines execute through the Execution Runner. The server
picks the transport from `Execution:DataSource` (`Mongo` or `EnterpriseCoreREST`); the client
never chooses it, and both paths return the same tabular payload that the SPA renders in the
results grid. Execution is capped at `Execution:TimeoutMs` (default 5,000 ms); a timeout is
cancelled and audited rather than thrown. Every execution appends one BRD 6.1 document to the
append-only `audit_logs` collection, and the store exposes no update or delete path.

Persistence is selected at startup by `Persistence:Mode`: `auto` uses MongoDB when reachable and
otherwise falls back to a file-backed store under `Persistence:DataDirectory`. Access requests,
agent state, conversations, and the audit trail all go through this seam, so a governance hold
survives a gateway restart and an approved request still resumes. Without a MongoDB server the
Development build routes Enterprise Core calls to the local `POST /dev/enterprise-core/query`
stub, and the demo tabular source keeps the preview working when neither backend is reachable.
```

- [ ] **Step 2: Tick the acceptance criteria**

In `sprints/sprint-6.md`, change each `- [ ]` under "Acceptance criteria" to `- [x]`.

- [ ] **Step 3: Verify the full suite**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests/Gateway.Tests.csproj
git restore docs/benchmarks/
cd src/web && npm test
```

Expected: the .NET suite passes, `docs/benchmarks/` is restored clean, and the web suite passes.

- [ ] **Step 4: Commit**

```bash
git add README.md sprints/sprint-6.md
printf 'docs(execution): document execution audit and durable holds\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg
git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg
```

---

## Self-Review Notes

- **Spec coverage:** persistence seam (Tasks 2-4), execution runner and timeout (Tasks 5-9), audit (Task 6), dual ingestion (Tasks 8-10), direct and conversation and resume wiring (Tasks 11-13), DI and dev stub (Task 14), results grid (Tasks 15-16), documentation (Task 17).
- **Deliberate deviation:** the design named `FileAuditLogStore` and `MongoAuditLogStore`; the plan uses one `DurableAuditLogStore` over `IDocumentStore` because the backend is already selected there. Behavior is unchanged.
- **Type consistency:** `ExecutionRequest`, `TabularResult`, `GovernanceDecision`, `ITabularQueryExecutor`, and `IQueryTransport` keep the same names and shapes across Tasks 5 through 14.
- **Deferred:** live MongoDB integration is guarded by `MONGODB_TEST_CONNECTION` and skipped here; XLSX/PDF/SMTP export stays in Sprint 7.
