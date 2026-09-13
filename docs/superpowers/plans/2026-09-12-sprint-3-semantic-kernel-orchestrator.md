# Sprint 3 — Semantic Kernel Orchestrator and Complex MQL Synthesis Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Semantic Kernel style orchestrator that, for complex English queries only, calls NVIDIA NIM to synthesize a validated MongoDB aggregation pipeline, retries on invalid output up to 3 times, streams agent status events over SSE, and shows that activity in the SPA.

**Architecture:** Keep the Sprint 2 deterministic pipeline intact. The router now marks every complex intent `ComplexLlmRequired`; a new `NlpOrchestrator` supervises: simple/cached/clarify paths pass through untouched, complex paths invoke an `ILlmQueryGenerator` (NVIDIA NIM via a Semantic Kernel chat-completion service) wrapped in a self-correcting loop that validates the produced pipeline with a light schema validator. Orchestrator stages publish `AgentEvent`s into an in-memory sink that the existing `GET /api/agents/stream` endpoint drains as SSE. An in-memory `IAgentStateStore` captures paused/governance state for later sprints. The SPA subscribes to the new SSE events and posts utterances to `/api/nlp/query`.

**Tech Stack:** .NET 10 (`net10.0`), ASP.NET Core Minimal APIs, `Microsoft.SemanticKernel` 1.80.1 + `Microsoft.SemanticKernel.Connectors.OpenAI` 1.80.1 (OpenAI-compatible, pointed at the NVIDIA NIM endpoint), `System.Threading.Channels`, xUnit 2.9.3 + `WebApplicationFactory`, Angular 21 standalone components with Vitest 4 + jsdom.

## Global Constraints

- Runtime floor: `net10.0` (gateway + tests). Do not change.
- LLM is invoked **only** for `NlpRouteKind.ComplexLlmRequired` (BRD-FR-03). Simple/cache/clarify paths must consume zero LLM tokens (Sprint 2 regression).
- Self-correction attempts: at most **3**. After the third invalid output, return a user-visible error and do not loop further (BRD-NFR-06).
- Clarification: never more than **one** follow-up question per turn (BRD-NFR-07).
- Silent fallbacks are exactly `limit 10`, `sort: rating_desc`, `market: All` (already `MqlDefaults.Standard`).
- Complex NVIDIA LLM MQL generation target latency is 1.5–3 s (BRD-NFR-02) — measured and recorded, not CI-asserted.
- `IAgentStateStore` is the paused-state persistence hook (BRD-NFR-13); in-memory only this sprint. No MongoDB.
- Never commit a real NVIDIA API key. `appsettings.json` holds the placeholder `TBD`; runtime override is env `NvidiaNim__ApiKey`. Tests that would call the live LLM must not run without a configured key.
- Pipeline validator is **light** (numeric `price`, array `amenities`). Full AST whitelist/write-block is Sprint 4.
- Assets under `src/gateway/Nlp/Assets/**` are content-copied to build output and resolved from `AppContext.BaseDirectory`.
- Follow existing conventions: file-scoped namespaces, folder-per-feature, records for DTOs, `Using Include="Xunit"` (no `using Xunit;`), tests construct services directly or use `GatewayFactory`.
- Do not commit `src/web/.vscode/`, `bin/`, `obj/`, or model binaries (already tracked model stays as-is).

---

### Task 1: NIM packages, options, and prompt assets

**Files:**
- Modify: `src/gateway/Gateway.csproj:13-31`
- Modify: `src/gateway/appsettings.json:18-22`
- Create: `src/gateway/Nlp/Llm/NvidiaNimOptions.cs`
- Create: `src/gateway/Nlp/Assets/Prompts/prompt_template.txt`
- Create: `src/gateway/Nlp/Assets/Prompts/schema.txt`
- Create: `src/gateway/Nlp/Assets/Prompts/sample.txt`
- Create: `src/gateway/Nlp/Assets/Prompts/examples.txt`
- Test: `tests/Gateway.Tests/Nlp/GatewayCsprojTests.cs` (add assertions)

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `Gateway.Nlp.Llm.NvidiaNimOptions` with `SectionName = "NvidiaNim"`, properties `BaseUrl`, `ApiKey`, `Model`, `MaxAttempts` (default 3), `TimeoutSeconds` (default 30), and `bool IsConfigured` (true only when `ApiKey` is non-empty and not `TBD`). Prompt files under `Nlp/Assets/Prompts/*.txt` copied to output.

- [ ] **Step 1: Add packages**

In `src/gateway/Gateway.csproj` add to the existing `<ItemGroup>` of `PackageReference`s:

```xml
    <PackageReference Include="Microsoft.SemanticKernel" Version="1.80.1" />
    <PackageReference Include="Microsoft.SemanticKernel.Connectors.OpenAI" Version="1.80.1" />
```

- [ ] **Step 2: Content-copy prompt assets**

Replace the second `<ItemGroup>` in `src/gateway/Gateway.csproj` with:

```xml
  <ItemGroup>
    <Content Update="Nlp/Assets/Gazetteers/*.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
    </Content>
    <Content Update="Nlp/Assets/Templates/*.scriban">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
    </Content>
    <Content Include="Nlp/Assets/Prompts/*.txt">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
    </Content>
  </ItemGroup>
```

- [ ] **Step 3: Add NIM options**

Create `src/gateway/Nlp/Llm/NvidiaNimOptions.cs`:

```csharp
namespace Gateway.Nlp.Llm;

public sealed class NvidiaNimOptions
{
    public const string SectionName = "NvidiaNim";

    public string BaseUrl { get; set; } = "https://integrate.api.nvidia.com/v1";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
    public int MaxAttempts { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 30;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.Equals(ApiKey, "TBD", StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 4: Add the four prompt assets**

Create `src/gateway/Nlp/Assets/Prompts/prompt_template.txt`:

```text
You generate MongoDB aggregation pipelines for a real-estate listings collection.
Convert the user request into a valid MongoDB aggregation pipeline.

Rules:
- Output ONLY a JSON array. No prose, no markdown fences.
- Allowed stages: $match, $sort, $limit, $group, $project, $unwind, $addFields, $count.
- Never emit write operators such as $out or $merge.
- Keep the pipeline minimal and use the provided schema field names exactly.

Schema:
{{schema}}

Examples:
{{examples}}

Extracted constraints (JSON):
{{slots}}

User request: {{utterance}}
{{previous_error}}
```

Create `src/gateway/Nlp/Assets/Prompts/schema.txt`:

```text
collection: sample_airbnb.listingsAndReviews
fields:
- price: number (daily price in USD)
- amenities: array<string>
- beds: number
- bedrooms: number
- address.market: string
- property_type: string
- review_scores.rating: number
```

Create `src/gateway/Nlp/Assets/Prompts/sample.txt`:

```text
Request: listings with pools in Los Angeles, just run it
Output: [{"$match":{"address.market":"Los Angeles","amenities":{"$all":["Pool"]}}},{"$sort":{"review_scores.rating":-1}},{"$limit":10}]
```

Create `src/gateway/Nlp/Assets/Prompts/examples.txt`:

```text
Request: average price by market
Output: [{"$group":{"_id":"$address.market","averagePrice":{"$avg":"$price"}}},{"$sort":{"averagePrice":-1}}]
Request: top 5 coziest listings near the beach
Output: [{"$match":{"amenities":{"$all":["Beach access"]}}},{"$sort":{"review_scores.rating":-1}},{"$limit":5}]
Request: count listings per property type in New York
Output: [{"$match":{"address.market":"New York"}},{"$group":{"_id":"$property_type","count":{"$sum":1}}},{"$sort":{"count":-1}}]
```

- [ ] **Step 5: Add config keys**

In `src/gateway/appsettings.json`, replace the `NvidiaNim` object with:

```json
  "NvidiaNim": {
    "BaseUrl": "https://integrate.api.nvidia.com/v1",
    "ApiKey": "TBD",
    "Model": "nvidia/nemotron-3-nano-omni-30b-a3b-reasoning",
    "MaxAttempts": 3,
    "TimeoutSeconds": 30
  },
```

- [ ] **Step 6: Extend the asset test**

Append to `Nlp_assets_are_copied_to_build_output` in `tests/Gateway.Tests/Nlp/GatewayCsprojTests.cs` (inside the method, after the existing asserts):

```csharp
        Assert.True(File.Exists(Path.Combine(baseDir, "Nlp", "Assets", "Prompts", "prompt_template.txt")));
        Assert.True(File.Exists(Path.Combine(baseDir, "Nlp", "Assets", "Prompts", "schema.txt")));
        Assert.True(File.Exists(Path.Combine(baseDir, "Nlp", "Assets", "Prompts", "sample.txt")));
        Assert.True(File.Exists(Path.Combine(baseDir, "Nlp", "Assets", "Prompts", "examples.txt")));
```

- [ ] **Step 7: Run the test**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests --filter FullyQualifiedName~GatewayCsprojTests
```

Expected: PASS (1 test).

- [ ] **Step 8: Commit**

```bash
git add src/gateway/Gateway.csproj src/gateway/appsettings.json src/gateway/Nlp/Llm/NvidiaNimOptions.cs src/gateway/Nlp/Assets/Prompts tests/Gateway.Tests/Nlp/GatewayCsprojTests.cs
git commit -m "Add NVIDIA NIM packages, options, and prompt assets"
```

---

### Task 2: Route all complex intents to the LLM path

**Files:**
- Modify: `src/gateway/Nlp/Router/NlpRouteResult.cs:6-23`
- Modify: `src/gateway/Nlp/Router/NlpRouter.cs:59-66`
- Modify: `src/gateway/Nlp/Http/NlpQueryResponse.cs:5-26`
- Test: `tests/Gateway.Tests/Nlp/NlpRouterTests.cs` (add a fact)

**Interfaces:**
- Consumes: nothing.
- Produces: `NlpRouteKind` gains `ComplexLlmFailed`. `NlpRouteResult` gains `int LlmAttempts = 0` and `string? Error = null` (at the end, so existing positional constructions still compile). `NlpQueryResponse` gains `int LlmAttempts` and `string? Error`, mapped in `From`.

- [ ] **Step 1: Write the failing test**

Add to `tests/Gateway.Tests/Nlp/NlpRouterTests.cs`:

```csharp
    [Fact]
    public void Complex_query_with_constraints_requires_llm()
    {
        var result = _router.Route("average price by market under $200");

        Assert.Equal(NlpRouteKind.ComplexLlmRequired, result.Kind);
        Assert.Null(result.Mql);
        Assert.Equal(0, result.LlmTokensConsumed);
        Assert.Equal(0, result.LlmAttempts);
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests --filter FullyQualifiedName~Complex_query_with_constraints_requires_llm
```

Expected: FAIL, `route.Kind` is `SimpleMql`.

- [ ] **Step 3: Update the result record and enum**

Replace `src/gateway/Nlp/Router/NlpRouteResult.cs` entirely:

```csharp
using Gateway.Nlp.Intent;
using Gateway.Nlp.Mql;

namespace Gateway.Nlp.Router;

public enum NlpRouteKind
{
    CacheHit,
    SimpleMql,
    ClarifyRequired,
    ComplexLlmRequired,
    ComplexLlmFailed
}

public sealed record NlpRouteResult(
    NlpRouteKind Kind,
    string? Mql,
    string? Question,
    bool SemanticCacheHit,
    bool SlotExtractionUsed,
    IntentKind Intent,
    bool JustRunIt,
    MqlDefaults ClarificationsApplied,
    int LlmTokensConsumed,
    int LlmAttempts = 0,
    string? Error = null);
```

- [ ] **Step 4: Change the complex condition**

In `src/gateway/Nlp/Router/NlpRouter.cs` replace the `if` at line 59:

```csharp
        if (intent.IsComplex)
        {
            return new NlpRouteResult(
                NlpRouteKind.ComplexLlmRequired, null, null,
                SemanticCacheHit: false, SlotExtractionUsed: slots.HasAnyConstraints,
                Intent: intent.Kind, JustRunIt: slots.JustRunIt,
                ClarificationsApplied: defaults, LlmTokensConsumed: 0);
        }
```

- [ ] **Step 5: Extend the HTTP DTO**

Replace `src/gateway/Nlp/Http/NlpQueryResponse.cs` entirely:

```csharp
using Gateway.Nlp.Router;

namespace Gateway.Nlp.Http;

public sealed record NlpQueryResponse(
    string Kind,
    string? Mql,
    string? Question,
    bool SemanticCacheHit,
    bool SlotExtractionUsed,
    string Intent,
    bool JustRunIt,
    int LlmTokensConsumed,
    int LlmAttempts,
    string? Error)
{
    public static NlpQueryResponse From(NlpRouteResult result)
    {
        return new NlpQueryResponse(
            result.Kind.ToString(),
            result.Mql,
            result.Question,
            result.SemanticCacheHit,
            result.SlotExtractionUsed,
            result.Intent.ToString(),
            result.JustRunIt,
            result.LlmTokensConsumed,
            result.LlmAttempts,
            result.Error);
    }
}
```

- [ ] **Step 6: Run the full test suite**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test MultiAgentMongoNlp.sln
```

Expected: PASS, 63 tests (62 existing + 1 new). `NlpRouterTests` and `ApiContractTests` still green (the response DTO in `ApiContractTests` ignores the two new JSON fields).

- [ ] **Step 7: Commit**

```bash
git add src/gateway/Nlp/Router src/gateway/Nlp/Http tests/Gateway.Tests/Nlp/NlpRouterTests.cs
git commit -m "Route complex intents to the LLM path and add attempts telemetry"
```

---

### Task 3: In-memory agent state store (S3-05)

**Files:**
- Create: `src/gateway/Nlp/Orchestrator/AgentState.cs`
- Create: `src/gateway/Nlp/Orchestrator/IAgentStateStore.cs`
- Create: `src/gateway/Nlp/Orchestrator/InMemoryAgentStateStore.cs`
- Test: `tests/Gateway.Tests/Nlp/AgentStateStoreTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `Gateway.Nlp.Orchestrator.AgentState(string SessionId, string Stage, string? PendingQuestion, DateTimeOffset UpdatedAt)` and `IAgentStateStore`:
  - `Task SaveAsync(AgentState state, CancellationToken cancellationToken = default)`
  - `Task<AgentState?> LoadAsync(string sessionId, CancellationToken cancellationToken = default)`

- [ ] **Step 1: Write the failing test**

Create `tests/Gateway.Tests/Nlp/AgentStateStoreTests.cs`:

```csharp
using Gateway.Nlp.Orchestrator;

namespace Gateway.Tests.Nlp;

public class AgentStateStoreTests
{
    [Fact]
    public async Task Save_then_load_round_trips_state()
    {
        var store = new InMemoryAgentStateStore();
        var state = new AgentState("sess_1", "paused", "Awaiting governance approval", DateTimeOffset.UnixEpoch);

        await store.SaveAsync(state);
        var loaded = await store.LoadAsync("sess_1");

        Assert.Equal(state, loaded);
    }

    [Fact]
    public async Task Load_unknown_session_returns_null()
    {
        var store = new InMemoryAgentStateStore();

        Assert.Null(await store.LoadAsync("missing"));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests --filter FullyQualifiedName~AgentStateStoreTests
```

Expected: FAIL to compile, `InMemoryAgentStateStore` does not exist.

- [ ] **Step 3: Implement the state store**

Create `src/gateway/Nlp/Orchestrator/AgentState.cs`:

```csharp
namespace Gateway.Nlp.Orchestrator;

public sealed record AgentState(
    string SessionId,
    string Stage,
    string? PendingQuestion,
    DateTimeOffset UpdatedAt);
```

Create `src/gateway/Nlp/Orchestrator/IAgentStateStore.cs`:

```csharp
namespace Gateway.Nlp.Orchestrator;

public interface IAgentStateStore
{
    Task SaveAsync(AgentState state, CancellationToken cancellationToken = default);
    Task<AgentState?> LoadAsync(string sessionId, CancellationToken cancellationToken = default);
}
```

Create `src/gateway/Nlp/Orchestrator/InMemoryAgentStateStore.cs`:

```csharp
using System.Collections.Concurrent;

namespace Gateway.Nlp.Orchestrator;

public sealed class InMemoryAgentStateStore : IAgentStateStore
{
    private readonly ConcurrentDictionary<string, AgentState> _states = new();

    public Task SaveAsync(AgentState state, CancellationToken cancellationToken = default)
    {
        _states[state.SessionId] = state;
        return Task.CompletedTask;
    }

    public Task<AgentState?> LoadAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        _states.TryGetValue(sessionId, out var state);
        return Task.FromResult(state);
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests --filter FullyQualifiedName~AgentStateStoreTests
```

Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/gateway/Nlp/Orchestrator/AgentState.cs src/gateway/Nlp/Orchestrator/IAgentStateStore.cs src/gateway/Nlp/Orchestrator/InMemoryAgentStateStore.cs tests/Gateway.Tests/Nlp/AgentStateStoreTests.cs
git commit -m "Add in-memory IAgentStateStore"
```

---

### Task 4: Prompt assembly and the NVIDIA NIM query generator (S3-02)

**Files:**
- Create: `src/gateway/Nlp/Llm/PromptAssets.cs`
- Create: `src/gateway/Nlp/Llm/LlmQueryResult.cs`
- Create: `src/gateway/Nlp/Llm/ILlmQueryGenerator.cs`
- Create: `src/gateway/Nlp/Llm/SemanticKernelLlmQueryGenerator.cs`
- Test: `tests/Gateway.Tests/Nlp/PromptAssetsTests.cs`

**Interfaces:**
- Consumes: `NvidiaNimOptions` (Task 1).
- Produces:
  - `PromptAssets.LoadFromDirectory(string directory)`; `string BuildPrompt(string utterance, string slotsJson, string? previousError)`.
  - `record LlmQueryResult(string Pipeline, int TokensConsumed)`.
  - `interface ILlmQueryGenerator { Task<LlmQueryResult> GenerateAsync(string utterance, string slotsJson, string? previousError, CancellationToken cancellationToken); }`.
  - `SemanticKernelLlmQueryGenerator(IOptions<NvidiaNimOptions> options, PromptAssets assets, ILogger<SemanticKernelLlmQueryGenerator> logger)` implementing `ILlmQueryGenerator`; throws `LlmNotConfiguredException` when `!options.Value.IsConfigured`.
  - `sealed class LlmNotConfiguredException : InvalidOperationException`.

- [ ] **Step 1: Write the failing test**

Create `tests/Gateway.Tests/Nlp/PromptAssetsTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests --filter FullyQualifiedName~PromptAssetsTests
```

Expected: FAIL to compile, `PromptAssets` does not exist.

- [ ] **Step 3: Implement prompt assets**

Create `src/gateway/Nlp/Llm/PromptAssets.cs`:

```csharp
namespace Gateway.Nlp.Llm;

public sealed class PromptAssets
{
    private const string ErrorHeader =
        "\nThe previous attempt was rejected. Fix this problem and return ONLY valid JSON:\n";

    public string PromptTemplate { get; }
    public string Schema { get; }
    public string Sample { get; }
    public string Examples { get; }

    private PromptAssets(string promptTemplate, string schema, string sample, string examples)
    {
        PromptTemplate = promptTemplate;
        Schema = schema;
        Sample = sample;
        Examples = examples;
    }

    public static PromptAssets LoadFromDirectory(string directory)
    {
        return new PromptAssets(
            File.ReadAllText(Path.Combine(directory, "prompt_template.txt")),
            File.ReadAllText(Path.Combine(directory, "schema.txt")),
            File.ReadAllText(Path.Combine(directory, "sample.txt")),
            File.ReadAllText(Path.Combine(directory, "examples.txt")));
    }

    public string BuildPrompt(string utterance, string slotsJson, string? previousError)
    {
        var errorBlock = string.IsNullOrWhiteSpace(previousError)
            ? string.Empty
            : ErrorHeader + previousError;

        return PromptTemplate
            .Replace("{{schema}}", Schema)
            .Replace("{{examples}}", Examples.TrimEnd() + Environment.NewLine + Sample.TrimEnd())
            .Replace("{{slots}}", slotsJson)
            .Replace("{{utterance}}", utterance)
            .Replace("{{previous_error}}", errorBlock);
    }
}
```

- [ ] **Step 4: Implement the generator**

Create `src/gateway/Nlp/Llm/LlmQueryResult.cs`:

```csharp
namespace Gateway.Nlp.Llm;

public sealed record LlmQueryResult(string Pipeline, int TokensConsumed);
```

Create `src/gateway/Nlp/Llm/ILlmQueryGenerator.cs`:

```csharp
namespace Gateway.Nlp.Llm;

public interface ILlmQueryGenerator
{
    Task<LlmQueryResult> GenerateAsync(
        string utterance,
        string slotsJson,
        string? previousError,
        CancellationToken cancellationToken);
}
```

Create `src/gateway/Nlp/Llm/SemanticKernelLlmQueryGenerator.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Gateway.Nlp.Llm;

public sealed class LlmNotConfiguredException : InvalidOperationException
{
    public LlmNotConfiguredException()
        : base("NVIDIA NIM is not configured. Set NvidiaNim:ApiKey (env NvidiaNim__ApiKey).")
    {
    }
}

public sealed class SemanticKernelLlmQueryGenerator : ILlmQueryGenerator
{
    private readonly NvidiaNimOptions _options;
    private readonly PromptAssets _assets;
    private readonly ILogger<SemanticKernelLlmQueryGenerator> _logger;
    private readonly Lazy<IChatCompletionService> _chat;

    public SemanticKernelLlmQueryGenerator(
        IOptions<NvidiaNimOptions> options,
        PromptAssets assets,
        ILogger<SemanticKernelLlmQueryGenerator> logger)
    {
        _options = options.Value;
        _assets = assets;
        _logger = logger;
        _chat = new Lazy<IChatCompletionService>(CreateChatService);
    }

    public async Task<LlmQueryResult> GenerateAsync(
        string utterance,
        string slotsJson,
        string? previousError,
        CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            throw new LlmNotConfiguredException();
        }

        var prompt = _assets.BuildPrompt(utterance, slotsJson, previousError);
        var history = new ChatHistory();
        history.AddUserMessage(prompt);

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0,
            MaxTokens = 1024
        };

        var response = await _chat.Value.GetChatMessageContentAsync(history, settings, cancellationToken: cancellationToken);
        var content = response.Content ?? string.Empty;
        _logger.LogInformation("NIM generated {Chars} chars of pipeline", content.Length);
        return new LlmQueryResult(content, EstimateTokens(prompt, content));
    }

    private IChatCompletionService CreateChatService()
    {
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(
            modelId: _options.Model,
            endpoint: new Uri(_options.BaseUrl),
            apiKey: _options.ApiKey);
        return builder.Build().GetRequiredService<IChatCompletionService>();
    }

    private static int EstimateTokens(string prompt, string completion)
    {
        return (prompt.Length + completion.Length) / 4;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests --filter FullyQualifiedName~PromptAssetsTests
```

Expected: PASS (3 tests). Confirm the project builds, which verifies the `AddOpenAIChatCompletion(modelId, endpoint, apiKey)` overload exists in SK 1.80.1. If the analyzer rejects the overload, replace the builder block with the equivalent `new OpenAI.OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(baseUrl) })` client-provider registration and record the change in the commit message.

- [ ] **Step 6: Commit**

```bash
git add src/gateway/Nlp/Llm tests/Gateway.Tests/Nlp/PromptAssetsTests.cs
git commit -m "Add prompt assembly and Semantic Kernel NIM query generator"
```

---

### Task 5: Pipeline validator and self-correction loop (S3-03, S3-04)

**Files:**
- Create: `src/gateway/Nlp/Llm/PipelineValidationResult.cs`
- Create: `src/gateway/Nlp/Llm/IPipelineValidator.cs`
- Create: `src/gateway/Nlp/Llm/PipelineValidator.cs`
- Create: `src/gateway/Nlp/Llm/SelfCorrectionResult.cs`
- Create: `src/gateway/Nlp/Llm/SelfCorrectingLlmQueryGenerator.cs`
- Test: `tests/Gateway.Tests/Nlp/PipelineValidatorTests.cs`
- Test: `tests/Gateway.Tests/Nlp/SelfCorrectionTests.cs`

**Interfaces:**
- Consumes: `ILlmQueryGenerator`, `NvidiaNimOptions` (Tasks 1, 4).
- Produces:
  - `record PipelineValidationResult(bool IsValid, string? Error)`.
  - `interface IPipelineValidator { PipelineValidationResult Validate(string pipeline); }`.
  - `record SelfCorrectionResult(string? Pipeline, int Attempts, int TokensConsumed, string? Error)`.
  - `SelfCorrectingLlmQueryGenerator(ILlmQueryGenerator generator, IPipelineValidator validator, IOptions<NvidiaNimOptions> options)` with `Task<SelfCorrectionResult> GenerateAsync(string utterance, string slotsJson, CancellationToken cancellationToken)`; attempts at most `options.Value.MaxAttempts`.

- [ ] **Step 1: Write the failing validator tests**

Create `tests/Gateway.Tests/Nlp/PipelineValidatorTests.cs`:

```csharp
using Gateway.Nlp.Llm;

namespace Gateway.Tests.Nlp;

public class PipelineValidatorTests
{
    private readonly IPipelineValidator _validator = new PipelineValidator();

    [Theory]
    [InlineData("""[{"$group":{"_id":"$address.market","averagePrice":{"$avg":"$price"}}}]""")]
    [InlineData("""[{"$match":{"price":{"$lte":200}}},{"$limit":10}]""")]
    [InlineData("""[{"$match":{"amenities":{"$all":["Pool"]}}}]""")]
    public void Accepts_valid_pipelines(string pipeline)
    {
        Assert.True(_validator.Validate(pipeline).IsValid);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"$match":{}}""")]
    [InlineData("""[{"$out":"other"}]""")]
    [InlineData("""[{"$match":{"price":"cheap"}}]""")]
    [InlineData("""[{"$match":{"amenities":"Pool"}}]""")]
    public void Rejects_invalid_pipelines(string pipeline)
    {
        var result = _validator.Validate(pipeline);

        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }
}
```

- [ ] **Step 2: Write the failing self-correction tests**

Create `tests/Gateway.Tests/Nlp/SelfCorrectionTests.cs`:

```csharp
using Gateway.Nlp.Llm;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Nlp;

public class SelfCorrectionTests
{
    private sealed class ScriptedGenerator : ILlmQueryGenerator
    {
        private readonly Queue<string> _outputs;
        public int Calls { get; private set; }

        public ScriptedGenerator(params string[] outputs) => _outputs = new Queue<string>(outputs);

        public Task<LlmQueryResult> GenerateAsync(string utterance, string slotsJson, string? previousError, CancellationToken cancellationToken = default)
        {
            Calls++;
            var output = _outputs.Count > 0 ? _outputs.Dequeue() : "not json";
            return Task.FromResult(new LlmQueryResult(output, 5));
        }
    }

    private static SelfCorrectingLlmQueryGenerator Sut(ILlmQueryGenerator generator, int maxAttempts = 3) =>
        new(generator, new PipelineValidator(), Options.Create(new NvidiaNimOptions { MaxAttempts = maxAttempts }));

    [Fact]
    public async Task Returns_first_valid_pipeline()
    {
        var generator = new ScriptedGenerator("not json", """[{"$limit":5}]""");

        var result = await Sut(generator).GenerateAsync("q", "{}", default);

        Assert.Equal(2, result.Attempts);
        Assert.Equal("""[{"$limit":5}]""", result.Pipeline);
        Assert.Null(result.Error);
        Assert.Equal(10, result.TokensConsumed);
    }

    [Fact]
    public async Task Stops_after_three_invalid_attempts()
    {
        var generator = new ScriptedGenerator("bad", "worse", "still bad", "never used");

        var result = await Sut(generator).GenerateAsync("q", "{}", default);

        Assert.Null(result.Pipeline);
        Assert.Equal(3, result.Attempts);
        Assert.Equal(3, generator.Calls);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public async Task Feeds_previous_error_back_to_generator()
    {
        string? seenError = null;
        var generator = new RecordingGenerator(error => seenError = error);

        await Sut(generator, maxAttempts: 2).GenerateAsync("q", "{}", default);

        Assert.False(string.IsNullOrWhiteSpace(seenError));
    }

    private sealed class RecordingGenerator : ILlmQueryGenerator
    {
        private readonly Action<string?> _onCall;

        public RecordingGenerator(Action<string?> onCall) => _onCall = onCall;

        public Task<LlmQueryResult> GenerateAsync(string utterance, string slotsJson, string? previousError, CancellationToken cancellationToken = default)
        {
            _onCall(previousError);
            return Task.FromResult(new LlmQueryResult("bad", 1));
        }
    }
}
```

- [ ] **Step 3: Run to verify they fail**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests --filter "FullyQualifiedName~PipelineValidatorTests|FullyQualifiedName~SelfCorrectionTests"
```

Expected: FAIL to compile, the new types do not exist.

- [ ] **Step 4: Implement the validator**

Create `src/gateway/Nlp/Llm/PipelineValidationResult.cs`:

```csharp
namespace Gateway.Nlp.Llm;

public sealed record PipelineValidationResult(bool IsValid, string? Error);
```

Create `src/gateway/Nlp/Llm/IPipelineValidator.cs`:

```csharp
namespace Gateway.Nlp.Llm;

public interface IPipelineValidator
{
    PipelineValidationResult Validate(string pipeline);
}
```

Create `src/gateway/Nlp/Llm/PipelineValidator.cs`:

```csharp
using System.Text.Json;

namespace Gateway.Nlp.Llm;

public sealed class PipelineValidator : IPipelineValidator
{
    private static readonly HashSet<string> AllowedStages = new(StringComparer.Ordinal)
    {
        "$match", "$sort", "$limit", "$group", "$project", "$unwind", "$addFields", "$count"
    };

    public PipelineValidationResult Validate(string pipeline)
    {
        if (string.IsNullOrWhiteSpace(pipeline))
        {
            return new PipelineValidationResult(false, "pipeline is empty");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(pipeline);
        }
        catch (JsonException ex)
        {
            return new PipelineValidationResult(false, $"invalid json: {ex.Message}");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new PipelineValidationResult(false, "pipeline must be a JSON array");
            }

            foreach (var stage in document.RootElement.EnumerateArray())
            {
                if (stage.ValueKind != JsonValueKind.Object)
                {
                    return new PipelineValidationResult(false, "each stage must be an object");
                }

                var properties = stage.EnumerateObject().ToList();
                if (properties.Count != 1 || !AllowedStages.Contains(properties[0].Name))
                {
                    return new PipelineValidationResult(false, "stage must have exactly one allowed operator");
                }

                var stageResult = ValidateStage(properties[0]);
                if (!stageResult.IsValid)
                {
                    return stageResult;
                }
            }
        }

        return new PipelineValidationResult(true, null);
    }

    private static PipelineValidationResult ValidateStage(JsonProperty stage)
    {
        if (stage.Name != "$match" || stage.Value.ValueKind != JsonValueKind.Object)
        {
            return new PipelineValidationResult(true, null);
        }

        if (stage.Value.TryGetProperty("price", out var price))
        {
            var numeric = price.ValueKind == JsonValueKind.Number ||
                          (price.ValueKind == JsonValueKind.Object &&
                           price.EnumerateObject().All(p => p.Value.ValueKind == JsonValueKind.Number));
            if (!numeric)
            {
                return new PipelineValidationResult(false, "price must be numeric");
            }
        }

        if (stage.Value.TryGetProperty("amenities", out var amenities))
        {
            var isArrayExpression = amenities.ValueKind == JsonValueKind.Object &&
                                    (amenities.TryGetProperty("$all", out var all) || amenities.TryGetProperty("$in", out all)) &&
                                    all.ValueKind == JsonValueKind.Array;
            if (!isArrayExpression)
            {
                return new PipelineValidationResult(false, "amenities must use $all or $in with an array");
            }
        }

        return new PipelineValidationResult(true, null);
    }
}
```

- [ ] **Step 5: Implement the self-correction loop**

Create `src/gateway/Nlp/Llm/SelfCorrectionResult.cs`:

```csharp
namespace Gateway.Nlp.Llm;

public sealed record SelfCorrectionResult(string? Pipeline, int Attempts, int TokensConsumed, string? Error);
```

Create `src/gateway/Nlp/Llm/SelfCorrectingLlmQueryGenerator.cs`:

```csharp
using Microsoft.Extensions.Options;

namespace Gateway.Nlp.Llm;

public sealed class SelfCorrectingLlmQueryGenerator
{
    private readonly ILlmQueryGenerator _generator;
    private readonly IPipelineValidator _validator;
    private readonly int _maxAttempts;

    public SelfCorrectingLlmQueryGenerator(
        ILlmQueryGenerator generator,
        IPipelineValidator validator,
        IOptions<NvidiaNimOptions> options)
    {
        _generator = generator;
        _validator = validator;
        _maxAttempts = Math.Max(1, options.Value.MaxAttempts);
    }

    public async Task<SelfCorrectionResult> GenerateAsync(
        string utterance,
        string slotsJson,
        CancellationToken cancellationToken)
    {
        var tokens = 0;
        string? previousError = null;

        for (var attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            var generated = await _generator.GenerateAsync(utterance, slotsJson, previousError, cancellationToken);
            tokens += generated.TokensConsumed;

            var validation = _validator.Validate(generated.Pipeline);
            if (validation.IsValid)
            {
                return new SelfCorrectionResult(generated.Pipeline, attempt, tokens, null);
            }

            previousError = validation.Error;
        }

        return new SelfCorrectionResult(null, _maxAttempts, tokens, previousError ?? "pipeline validation failed");
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests --filter "FullyQualifiedName~PipelineValidatorTests|FullyQualifiedName~SelfCorrectionTests"
```

Expected: PASS (5 validator + 3 self-correction tests).

- [ ] **Step 7: Commit**

```bash
git add src/gateway/Nlp/Llm tests/Gateway.Tests/Nlp/PipelineValidatorTests.cs tests/Gateway.Tests/Nlp/SelfCorrectionTests.cs
git commit -m "Add light pipeline validator and 3-attempt self-correction loop"
```

---

### Task 6: Orchestrator supervisor and agent event sink (S3-01)

**Files:**
- Create: `src/gateway/Nlp/Orchestrator/AgentEvent.cs`
- Create: `src/gateway/Nlp/Orchestrator/IAgentEventSink.cs`
- Create: `src/gateway/Nlp/Orchestrator/InMemoryAgentEventSink.cs`
- Create: `src/gateway/Nlp/Orchestrator/INlpOrchestrator.cs`
- Create: `src/gateway/Nlp/Orchestrator/NlpOrchestrator.cs`
- Test: `tests/Gateway.Tests/Nlp/NlpOrchestratorTests.cs`

**Interfaces:**
- Consumes: `INlpRouter`, `Gazetteer`, `SlotExtractor`, `SelfCorrectingLlmQueryGenerator`, `IAgentStateStore`, `IAgentEventSink` (Tasks 3, 5).
- Produces:
  - `record AgentEvent(string Name, string Status, string? Detail = null)`.
  - `interface IAgentEventSink { void Publish(AgentEvent agentEvent); IAsyncEnumerable<AgentEvent> ReadAllAsync(CancellationToken cancellationToken); }`.
  - `interface INlpOrchestrator { Task<NlpRouteResult> OrchestrateAsync(string utterance, CancellationToken cancellationToken = default); }`.
  - `NlpOrchestrator(INlpRouter router, Gazetteer gazetteer, SelfCorrectingLlmQueryGenerator generator, IAgentEventSink events, IAgentStateStore stateStore)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/Gateway.Tests/Nlp/NlpOrchestratorTests.cs`:

```csharp
using Gateway.Nlp.Abstractions;
using Gateway.Nlp.Cache;
using Gateway.Nlp.Llm;
using Gateway.Nlp.Mql;
using Gateway.Nlp.Orchestrator;
using Gateway.Nlp.Router;
using Gateway.Nlp.Slots;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Nlp;

public class NlpOrchestratorTests
{
    private sealed class StubGenerator : ILlmQueryGenerator
    {
        private readonly string _pipeline;
        public int Calls { get; private set; }

        public StubGenerator(string pipeline) => _pipeline = pipeline;

        public Task<LlmQueryResult> GenerateAsync(string utterance, string slotsJson, string? previousError, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new LlmQueryResult(_pipeline, 7));
        }
    }

    private sealed class VectorEmbedder : ITextEmbedder
    {
        public float[] Embed(string text) => new float[] { 1, 0, 0, 0, 0, 0, 0, 0 };
    }

    private static (NlpOrchestrator Sut, StubGenerator Generator, InMemoryAgentEventSink Events) Build(string pipeline)
    {
        var gazetteer = Gazetteer.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Gazetteers"));
        var builder = ScribanSimpleMqlBuilder.FromAssetsDirectory(Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Templates"));
        var router = new NlpRouter(new VectorEmbedder(), new SemanticCache(), builder, gazetteer);
        var generator = new StubGenerator(pipeline);
        var events = new InMemoryAgentEventSink();
        var corrector = new SelfCorrectingLlmQueryGenerator(generator, new PipelineValidator(),
            Options.Create(new NvidiaNimOptions { MaxAttempts = 3 }));
        var sut = new NlpOrchestrator(router, gazetteer, corrector, events, new InMemoryAgentStateStore());
        return (sut, generator, events);
    }

    [Fact]
    public async Task Simple_query_skips_llm_and_emits_started_and_completed()
    {
        var (sut, generator, events) = Build("""[{"$limit":5}]""");

        var result = await sut.OrchestrateAsync("listings with pools in Los Angeles, just run it");

        Assert.Equal(NlpRouteKind.SimpleMql, result.Kind);
        Assert.Equal(0, generator.Calls);
        var names = await Read(events, 2);
        Assert.Equal(["agent.started", "agent.completed"], names);
    }

    [Fact]
    public async Task Complex_query_synthesizes_pipeline_with_llm()
    {
        var (sut, generator, _) = Build("""[{"$group":{"_id":"$address.market","averagePrice":{"$avg":"$price"}}}]""");

        var result = await sut.OrchestrateAsync("average price by market");

        Assert.Equal(NlpRouteKind.ComplexLlmRequired, result.Kind);
        Assert.Contains("$group", result.Mql);
        Assert.Equal(1, generator.Calls);
        Assert.Equal(1, result.LlmAttempts);
        Assert.True(result.LlmTokensConsumed > 0);
    }

    [Fact]
    public async Task Complex_query_failing_validation_returns_error_after_three_attempts()
    {
        var (sut, generator, _) = Build("not json");

        var result = await sut.OrchestrateAsync("average price by market");

        Assert.Equal(NlpRouteKind.ComplexLlmFailed, result.Kind);
        Assert.Null(result.Mql);
        Assert.Equal(3, result.LlmAttempts);
        Assert.Equal(3, generator.Calls);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public async Task Clarify_emits_single_clarifying_event()
    {
        var (sut, _, events) = Build("""[{"$limit":5}]""");

        var result = await sut.OrchestrateAsync("what can you do?");

        Assert.Equal(NlpRouteKind.ClarifyRequired, result.Kind);
        Assert.NotNull(result.Question);
        var names = await Read(events, 2);
        Assert.Equal(["agent.started", "agent.clarifying"], names);
    }

    private static async Task<List<string>> Read(IAgentEventSink sink, int count)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var names = new List<string>();
        await foreach (var agentEvent in sink.ReadAllAsync(cts.Token))
        {
            names.Add(agentEvent.Name);
            if (names.Count == count)
            {
                break;
            }
        }

        return names;
    }
}
```

- [ ] **Step 2: Run to verify they fail**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests --filter FullyQualifiedName~NlpOrchestratorTests
```

Expected: FAIL to compile, the orchestrator types do not exist.

- [ ] **Step 3: Implement the event sink**

Create `src/gateway/Nlp/Orchestrator/AgentEvent.cs`:

```csharp
namespace Gateway.Nlp.Orchestrator;

public sealed record AgentEvent(string Name, string Status, string? Detail = null);
```

Create `src/gateway/Nlp/Orchestrator/IAgentEventSink.cs`:

```csharp
namespace Gateway.Nlp.Orchestrator;

public interface IAgentEventSink
{
    void Publish(AgentEvent agentEvent);

    IAsyncEnumerable<AgentEvent> ReadAllAsync(CancellationToken cancellationToken);
}
```

Create `src/gateway/Nlp/Orchestrator/InMemoryAgentEventSink.cs`:

```csharp
using System.Threading.Channels;

namespace Gateway.Nlp.Orchestrator;

public sealed class InMemoryAgentEventSink : IAgentEventSink
{
    private readonly Channel<AgentEvent> _channel = Channel.CreateUnbounded<AgentEvent>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

    public void Publish(AgentEvent agentEvent)
    {
        _channel.Writer.TryWrite(agentEvent);
    }

    public IAsyncEnumerable<AgentEvent> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
```

- [ ] **Step 4: Implement the orchestrator**

Create `src/gateway/Nlp/Orchestrator/INlpOrchestrator.cs`:

```csharp
using Gateway.Nlp.Router;

namespace Gateway.Nlp.Orchestrator;

public interface INlpOrchestrator
{
    Task<NlpRouteResult> OrchestrateAsync(string utterance, string sessionId = "anonymous", CancellationToken cancellationToken = default);
}
```

Create `src/gateway/Nlp/Orchestrator/NlpOrchestrator.cs`:

```csharp
using System.Text.Json;
using Gateway.Nlp.Llm;
using Gateway.Nlp.Mql;
using Gateway.Nlp.Router;
using Gateway.Nlp.Slots;

namespace Gateway.Nlp.Orchestrator;

public sealed class NlpOrchestrator : INlpOrchestrator
{
    private readonly INlpRouter _router;
    private readonly Gazetteer _gazetteer;
    private readonly SelfCorrectingLlmQueryGenerator _generator;
    private readonly IAgentEventSink _events;
    private readonly IAgentStateStore _stateStore;

    public NlpOrchestrator(
        INlpRouter router,
        Gazetteer gazetteer,
        SelfCorrectingLlmQueryGenerator generator,
        IAgentEventSink events,
        IAgentStateStore stateStore)
    {
        _router = router;
        _gazetteer = gazetteer;
        _generator = generator;
        _events = events;
        _stateStore = stateStore;
    }

    public async Task<NlpRouteResult> OrchestrateAsync(
        string utterance,
        string sessionId = "anonymous",
        CancellationToken cancellationToken = default)
    {
        _events.Publish(new AgentEvent("agent.started", "started", utterance));

        var routed = _router.Route(utterance);

        if (routed.Kind == NlpRouteKind.ClarifyRequired)
        {
            _events.Publish(new AgentEvent("agent.clarifying", "clarifying", routed.Question));
            await _stateStore.SaveAsync(
                new AgentState(sessionId, "clarifying", routed.Question, DateTimeOffset.UtcNow),
                cancellationToken);
            return routed;
        }

        if (routed.Kind != NlpRouteKind.ComplexLlmRequired)
        {
            _events.Publish(new AgentEvent("agent.completed", "completed", routed.Kind.ToString()));
            return routed;
        }

        var slots = SlotExtractor.Extract(utterance, _gazetteer);
        var slotsJson = JsonSerializer.Serialize(slots);

        SelfCorrectionResult corrected;
        try
        {
            corrected = await _generator.GenerateAsync(utterance, slotsJson, cancellationToken);
        }
        catch (Exception ex)
        {
            _events.Publish(new AgentEvent("agent.completed", "failed", ex.Message));
            return routed with
            {
                Kind = NlpRouteKind.ComplexLlmFailed,
                LlmAttempts = 1,
                Error = ex.Message
            };
        }

        if (corrected.Pipeline is null)
        {
            _events.Publish(new AgentEvent("agent.completed", "failed", corrected.Error));
            return routed with
            {
                Kind = NlpRouteKind.ComplexLlmFailed,
                LlmAttempts = corrected.Attempts,
                LlmTokensConsumed = corrected.TokensConsumed,
                Error = corrected.Error
            };
        }

        _events.Publish(new AgentEvent("agent.completed", "completed", "ComplexLlmRequired"));
        return routed with
        {
            Mql = corrected.Pipeline,
            LlmAttempts = corrected.Attempts,
            LlmTokensConsumed = corrected.TokensConsumed
        };
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests --filter FullyQualifiedName~NlpOrchestratorTests
```

Expected: PASS (4 tests).

- [ ] **Step 6: Commit**

```bash
git add src/gateway/Nlp/Orchestrator tests/Gateway.Tests/Nlp/NlpOrchestratorTests.cs
git commit -m "Add NLP orchestrator supervisor and in-memory agent event sink"
```

---

### Task 7: DI wiring, orchestrator endpoint, and SSE agent events

**Files:**
- Modify: `src/gateway/Nlp/NlpServiceCollectionExtensions.cs:12-34`
- Modify: `src/gateway/Program.cs:118-158`
- Test: `tests/Gateway.Tests/ApiContractTests.cs` (update `NlpQueryDto`, add tests)

**Interfaces:**
- Consumes: all new types from Tasks 1-6.
- Produces: `AddGatewayNlp` also registers `Gazetteer` (singleton), `PromptAssets` (singleton, loaded from `AppContext.BaseDirectory/Nlp/Assets/Prompts`), `IPipelineValidator`, `ILlmQueryGenerator`, `SelfCorrectingLlmQueryGenerator`, `IAgentEventSink`, `IAgentStateStore`, and `INlpOrchestrator`. `POST /api/nlp/query` now uses `INlpOrchestrator`. `GET /api/agents/stream` emits `agent.idle` on connect, then relays every published `AgentEvent`.

- [ ] **Step 1: Write the failing integration tests**

In `tests/Gateway.Tests/ApiContractTests.cs`, replace the `NlpQueryDto` record (lines 186-194) with:

```csharp
    private sealed record NlpQueryDto(
        string Kind,
        string? Mql,
        string? Question,
        bool SemanticCacheHit,
        bool SlotExtractionUsed,
        string Intent,
        bool JustRunIt,
        int LlmTokensConsumed,
        int LlmAttempts,
        string? Error);
```

Then add these tests before the `AuthedClient()` helper:

```csharp
    [Fact]
    public async Task Nlp_query_complex_utterance_reports_llm_attempt_telemetry()
    {
        var client = AuthedClient();

        var response = await client.PostAsJsonAsync("/api/nlp/query", new { utterance = "average price by market" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<NlpQueryDto>();
        Assert.NotNull(payload);
        Assert.True(payload!.Kind is "ComplexLlmRequired" or "ComplexLlmFailed");
    }

    [Fact]
    public async Task Agent_stream_event_names_are_well_formed()
    {
        var client = AuthedClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/agents/stream");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream);
        var first = await reader.ReadLineAsync(cts.Token);

        Assert.Equal("event: agent.idle", first);
    }
```

- [ ] **Step 2: Run to verify failure**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests --filter FullyQualifiedName~ApiContractTests
```

Expected: FAIL, the endpoint still returns `SimpleMql` for the complex utterance and/or the build fails because `NlpQueryResponse` changed.

- [ ] **Step 3: Wire DI**

Replace `src/gateway/Nlp/NlpServiceCollectionExtensions.cs` entirely:

```csharp
using Gateway.Nlp.Abstractions;
using Gateway.Nlp.Cache;
using Gateway.Nlp.Embeddings;
using Gateway.Nlp.Llm;
using Gateway.Nlp.Mql;
using Gateway.Nlp.Orchestrator;
using Gateway.Nlp.Router;
using Gateway.Nlp.Slots;
using Microsoft.Extensions.Options;

namespace Gateway.Nlp;

public static class NlpServiceCollectionExtensions
{
    public static IServiceCollection AddGatewayNlp(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NlpOptions>(configuration.GetSection(NlpOptions.SectionName));
        services.Configure<NvidiaNimOptions>(configuration.GetSection(NvidiaNimOptions.SectionName));

        services.AddSingleton<ITextEmbedder>(sp =>
        {
            var env = sp.GetRequiredService<IHostEnvironment>();
            var options = sp.GetRequiredService<IOptions<NlpOptions>>().Value;
            var modelPath = Resolve(env, options.Embeddings.ModelPath);
            var vocabPath = Resolve(env, options.Embeddings.TokenizerPath);
            return new OnnxBgeSmallEmbedder(modelPath, vocabPath, options.Embeddings.MaxTokens);
        });

        services.AddSingleton<ISemanticCache, SemanticCache>();
        services.AddSingleton<IMqlBuilder>(_ => ScribanSimpleMqlBuilder.FromAssetsDirectory(
            Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Templates")));
        services.AddSingleton(Gazetteer.LoadFromDirectory(
            Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Gazetteers")));
        services.AddSingleton(PromptAssets.LoadFromDirectory(
            Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Prompts")));

        services.AddSingleton<INlpRouter>(sp => new NlpRouter(
            sp.GetRequiredService<ITextEmbedder>(),
            sp.GetRequiredService<ISemanticCache>(),
            sp.GetRequiredService<IMqlBuilder>(),
            sp.GetRequiredService<Gazetteer>()));

        services.AddSingleton<IPipelineValidator, PipelineValidator>();
        services.AddSingleton<ILlmQueryGenerator, SemanticKernelLlmQueryGenerator>();
        services.AddSingleton<SelfCorrectingLlmQueryGenerator>();
        services.AddSingleton<IAgentEventSink, InMemoryAgentEventSink>();
        services.AddSingleton<IAgentStateStore, InMemoryAgentStateStore>();
        services.AddSingleton<INlpOrchestrator, NlpOrchestrator>();

        return services;
    }

    private static string Resolve(IHostEnvironment env, string path)
    {
        return Path.IsPathRooted(path) ? path : Path.Combine(env.ContentRootPath, path);
    }
}
```

- [ ] **Step 4: Update the endpoint and SSE stream**

In `src/gateway/Program.cs`, add the orchestrator using (next to `using Gateway.Nlp.Router;`):

```csharp
using Gateway.Nlp.Orchestrator;
```

Replace the `/api/nlp/query` mapping (lines 118-132) with:

```csharp
app.MapPost("/api/nlp/query", async (NlpQueryRequest request, ClaimsPrincipal user, INlpOrchestrator orchestrator, CancellationToken cancellationToken) =>
{
    var utterance = request.Utterance?.Trim();
    if (string.IsNullOrWhiteSpace(utterance))
    {
        return Results.BadRequest(new { error = "utterance is required" });
    }

    if (utterance.Length > 500)
    {
        return Results.BadRequest(new { error = "utterance must be 500 characters or fewer" });
    }

    var sessionId = user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";
    var result = await orchestrator.OrchestrateAsync(utterance, sessionId, cancellationToken);
    return Results.Ok(NlpQueryResponse.From(result));
}).RequireAuthorization();
```

Replace the `/api/agents/stream` mapping (lines 134-158) with:

```csharp
app.MapGet("/api/agents/stream", async (HttpContext context, IAgentEventSink events, CancellationToken cancellationToken) =>
{
    context.Response.Headers.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers.Connection = "keep-alive";

    await WriteAgentEvent(context, new AgentEvent("agent.idle", "idle"), cancellationToken);

    try
    {
        await foreach (var agentEvent in events.ReadAllAsync(cancellationToken))
        {
            await WriteAgentEvent(context, agentEvent, cancellationToken);
        }
    }
    catch (OperationCanceledException)
    {
    }
}).RequireAuthorization();
```

Immediately before `public partial class Program;` (currently line 162), add the helper:

```csharp
static async Task WriteAgentEvent(HttpContext context, AgentEvent agentEvent, CancellationToken cancellationToken)
{
    var payload = JsonSerializer.Serialize(new { status = agentEvent.Status, detail = agentEvent.Detail });
    await context.Response.WriteAsync($"event: {agentEvent.Name}\n", cancellationToken);
    await context.Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
    await context.Response.Body.FlushAsync(cancellationToken);
}
```

- [ ] **Step 5: Run the full suite**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test MultiAgentMongoNlp.sln
```

Expected: PASS. Simple queries still return `SimpleMql` with zero tokens; the complex query returns `ComplexLlmFailed` in this environment because `NvidiaNim:ApiKey` is `TBD` (no network). This is the expected, keyless behavior.

- [ ] **Step 6: Commit**

```bash
git add src/gateway/Nlp/NlpServiceCollectionExtensions.cs src/gateway/Program.cs tests/Gateway.Tests/ApiContractTests.cs
git commit -m "Wire orchestrator into DI, endpoint, and SSE agent stream"
```

---

### Task 8: SPA agent activity and query wiring

**Files:**
- Modify: `src/web/src/app/services/agent-stream.service.ts:17-39`
- Create: `src/web/src/app/models/nlp-query-response.ts`
- Create: `src/web/src/app/services/nlp-query.service.ts`
- Modify: `src/web/src/app/app.component.ts:1-57`
- Modify: `src/web/src/app/app.component.html:20-24`
- Test: `src/web/src/app/services/nlp-query.service.spec.ts`
- Test: `src/web/src/app/app.component.spec.ts:26-32` (add provider)

**Interfaces:**
- Consumes: gateway `POST /api/nlp/query`, `GET /api/agents/stream` events `agent.started`, `agent.clarifying`, `agent.completed`.
- Produces:
  - `AgentStreamService.connect()` emits every listed event.
  - `NlpQueryService.query(utterance: string): Observable<NlpQueryResponse>`.
  - `NlpQueryResponse` interface `{ kind, mql, question, semanticCacheHit, slotExtractionUsed, intent, justRunIt, llmTokensConsumed, llmAttempts, error }`.
  - `AppComponent` gains `queryText`, `queryResult`, `agentActivity`, and `runQuery()`.

- [ ] **Step 1: Write the failing service test**

Create `src/web/src/app/services/nlp-query.service.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { NlpQueryService } from './nlp-query.service';

describe('NlpQueryService', () => {
  let service: NlpQueryService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(NlpQueryService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('posts the utterance and returns the pipeline', () => {
    service.query('average price by market').subscribe((result) => {
      expect(result.kind).toBe('ComplexLlmRequired');
      expect(result.mql).toContain('$group');
    });

    const req = http.expectOne('/api/nlp/query');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ utterance: 'average price by market' });
    req.flush({
      kind: 'ComplexLlmRequired',
      mql: '[{"$group":{"_id":"$address.market"}}]',
      question: null,
      semanticCacheHit: false,
      slotExtractionUsed: false,
      intent: 'Search',
      justRunIt: false,
      llmTokensConsumed: 42,
      llmAttempts: 1,
      error: null
    });
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
npm test
```

Working directory: `src/web`. Expected: FAIL, `nlp-query.service` does not exist.

- [ ] **Step 3: Implement the model and service**

Create `src/web/src/app/models/nlp-query-response.ts`:

```typescript
export interface NlpQueryResponse {
  kind: string;
  mql: string | null;
  question: string | null;
  semanticCacheHit: boolean;
  slotExtractionUsed: boolean;
  intent: string;
  justRunIt: boolean;
  llmTokensConsumed: number;
  llmAttempts: number;
  error: string | null;
}
```

Create `src/web/src/app/services/nlp-query.service.ts`:

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { NlpQueryResponse } from '../models/nlp-query-response';

@Injectable({ providedIn: 'root' })
export class NlpQueryService {
  constructor(private readonly http: HttpClient) {}

  query(utterance: string): Observable<NlpQueryResponse> {
    return this.http.post<NlpQueryResponse>('/api/nlp/query', { utterance });
  }
}
```

- [ ] **Step 4: Extend the SSE service**

Replace `connect()` in `src/web/src/app/services/agent-stream.service.ts` with:

```typescript
  connect(): Observable<AgentEvent> {
    return new Observable((subscriber) => {
      const token = this.session.token();
      const params = new URLSearchParams();
      if (token) {
        params.set('access_token', token);
      }
      const source = new EventSource(`/api/agents/stream?${params.toString()}`);
      const names = ['agent.idle', 'agent.started', 'agent.clarifying', 'agent.completed'];

      const handlers = names.map((name) => {
        const handler = (event: MessageEvent) => {
          this.zone.run(() => subscriber.next({ event: name, data: event.data }));
        };
        source.addEventListener(name, handler as EventListener);
        return { name, handler };
      });

      source.onerror = () => {
        this.zone.run(() => subscriber.error(new Error('sse-disconnected')));
      };

      return () => {
        for (const { name, handler } of handlers) {
          source.removeEventListener(name, handler as EventListener);
        }
        source.close();
      };
    });
  }
```

- [ ] **Step 5: Wire the component**

Replace `src/web/src/app/app.component.ts` entirely:

```typescript
import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { retry, Subscription, switchMap, timer } from 'rxjs';
import { Greeting } from './models/greeting';
import { NlpQueryResponse } from './models/nlp-query-response';
import { AgentStreamService } from './services/agent-stream.service';
import { NlpQueryService } from './services/nlp-query.service';
import { SessionService } from './services/session.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit, OnDestroy {
  greeting: Greeting | null = null;
  streamStatus = 'connecting';
  agentActivity: string[] = [];
  queryText = '';
  queryResult: NlpQueryResponse | null = null;
  error: string | null = null;
  private sub = new Subscription();

  constructor(
    private readonly session: SessionService,
    private readonly agents: AgentStreamService,
    private readonly nlp: NlpQueryService
  ) {}

  ngOnInit(): void {
    this.session.sessionId();
    this.sub.add(
      this.session.bootstrap().pipe(switchMap(() => this.session.greeting())).subscribe({
        next: (greeting) => {
          this.greeting = greeting;
          this.listen();
        },
        error: () => {
          this.error = 'Unable to load session. Start the ASP.NET Core gateway on port 5235.';
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }

  runQuery(): void {
    const utterance = this.queryText.trim();
    if (!utterance) {
      return;
    }

    this.sub.add(
      this.nlp.query(utterance).subscribe({
        next: (result) => {
          this.queryResult = result;
          this.streamStatus = result.kind;
        },
        error: () => {
          this.error = 'Query failed. Is the gateway running?';
        }
      })
    );
  }

  private listen(): void {
    this.sub.add(
      this.agents.connect().pipe(retry({ delay: () => timer(2000) })).subscribe({
        next: (event) => {
          this.streamStatus = event.event;
          this.agentActivity = [...this.agentActivity.slice(-4), event.event];
        },
        error: () => {
          this.streamStatus = 'reconnecting';
        }
      })
    );
  }
}
```

- [ ] **Step 6: Update the template**

Replace the `<section class="telemetry">` block in `src/web/src/app/app.component.html` with:

```html
  <section class="telemetry">
    <h2>Agent swarm</h2>
    <p>Transport: Server-Sent Events</p>
    <p class="status">Status: {{ streamStatus }}</p>
    <ul class="activity">
      @for (event of agentActivity; track $index) {
        <li>{{ event }}</li>
      }
    </ul>
    <form (ngSubmit)="runQuery()">
      <input name="utterance" [(ngModel)]="queryText" placeholder="Ask about listings" />
      <button type="submit">Run query</button>
    </form>
    @if (queryResult) {
      <pre class="pipeline">{{ queryResult.mql ?? queryResult.error }}</pre>
    }
  </section>
```

- [ ] **Step 7: Add the component test provider**

In `src/web/src/app/app.component.spec.ts`, add `NlpQueryService` to the provider list (after the `AgentStreamService` provider) and import it:

```typescript
import { NlpQueryService } from './services/nlp-query.service';
```

```typescript
        {
          provide: NlpQueryService,
          useValue: {
            query: () => of({
              kind: 'SimpleMql',
              mql: '[{"$limit":10}]',
              question: null,
              semanticCacheHit: false,
              slotExtractionUsed: true,
              intent: 'Search',
              justRunIt: false,
              llmTokensConsumed: 0,
              llmAttempts: 0,
              error: null
            })
          }
        }
```

- [ ] **Step 8: Run the SPA tests**

Run:

```bash
npm test
```

Working directory: `src/web`. Expected: PASS (3 test files). If the production build is desired: `npm run build` also succeeds.

- [ ] **Step 9: Commit**

```bash
git add src/web/src/app
git commit -m "Show streamed agent activity and wire SPA NLP query"
```

---

### Task 9: Complex-path benchmark and sprint docs (BRD-NFR-02)

**Files:**
- Create: `tests/Gateway.Tests/Nlp/Sprint3BenchTests.cs`
- Create (generated by test): `docs/benchmarks/sprint-3-nfr02.md`
- Modify: `sprints/sprint-3.md` (tick acceptance checkboxes that are now covered)

**Interfaces:**
- Consumes: `NlpOrchestrator`, `SelfCorrectingLlmQueryGenerator`, `PipelineValidator`.
- Produces: a recorded benchmark of orchestration overhead (excluding network latency) and a generated results table.

- [ ] **Step 1: Write the benchmark test**

Create `tests/Gateway.Tests/Nlp/Sprint3BenchTests.cs`:

```csharp
using System.Diagnostics;
using Gateway.Nlp.Abstractions;
using Gateway.Nlp.Cache;
using Gateway.Nlp.Llm;
using Gateway.Nlp.Mql;
using Gateway.Nlp.Orchestrator;
using Gateway.Nlp.Router;
using Gateway.Nlp.Slots;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Nlp;

public class Sprint3BenchTests
{
    private const int Iterations = 200;

    private sealed class ConstEmbedder : ITextEmbedder
    {
        public float[] Embed(string text) => new float[] { 1, 0, 0, 0, 0, 0, 0, 0 };
    }

    private sealed class FixedGenerator : ILlmQueryGenerator
    {
        public Task<LlmQueryResult> GenerateAsync(string utterance, string slotsJson, string? previousError, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LlmQueryResult("""[{"$group":{"_id":"$address.market","averagePrice":{"$avg":"$price"}}}]""", 40));
        }
    }

    [Fact]
    public void Records_orchestration_overhead_for_complex_queries()
    {
        var gazetteer = Gazetteer.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Gazetteers"));
        var builder = ScribanSimpleMqlBuilder.FromAssetsDirectory(Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Templates"));
        var router = new NlpRouter(new ConstEmbedder(), new SemanticCache(), builder, gazetteer);
        var corrector = new SelfCorrectingLlmQueryGenerator(new FixedGenerator(), new PipelineValidator(),
            Options.Create(new NvidiaNimOptions { MaxAttempts = 3 }));
        var orchestrator = new NlpOrchestrator(router, gazetteer, corrector, new InMemoryAgentEventSink(), new InMemoryAgentStateStore());

        // warm
        for (var i = 0; i < 10; i++)
        {
            orchestrator.OrchestrateAsync("average price by market under $200").GetAwaiter().GetResult();
        }

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < Iterations; i++)
        {
            orchestrator.OrchestrateAsync("average price by market under $200").GetAwaiter().GetResult();
        }
        sw.Stop();
        var perCall = sw.Elapsed.TotalMilliseconds / Iterations;

        var content =
            "# Sprint 3 — BRD-NFR-02 Complex MQL Benchmarks\n" +
            "\n" +
            "Orchestration overhead only (validator + self-correction loop with a fixed in-process generator).\n" +
            "Live NVIDIA NIM network latency (budget 1.5–3 s) is measured manually with a configured key.\n" +
            "\n" +
            "| Step | Measured | Budget |\n" +
            "| --- | --- | --- |\n" +
            $"| complex route (validation + single attempt, no network) | {perCall:F2} ms | n/a (excludes LLM) |\n";

        var path = Path.Combine(TestPaths.RepoRoot(), "docs", "benchmarks", "sprint-3-nfr02.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);

        Assert.True(perCall < 50, $"orchestration overhead {perCall:F2} ms exceeded 50 ms");
    }
}
```

- [ ] **Step 2: Run the benchmark test**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test tests/Gateway.Tests --filter FullyQualifiedName~Sprint3BenchTests
```

Expected: PASS, and `docs/benchmarks/sprint-3-nfr02.md` is created.

- [ ] **Step 3: Restore the generated doc only after reviewing**

The test rewrites `docs/benchmarks/sprint-3-nfr02.md` on every run. If it was already committed, restore any incidental numeric churn before committing:

```bash
git checkout -- docs/benchmarks/sprint-3-nfr02.md
```

- [ ] **Step 4: Tick covered acceptance criteria**

In `sprints/sprint-3.md`, mark these checkboxes `[x]` (leave the SPA/steaming one ticked only if Task 8 tests pass):

```markdown
- [x] Complex English queries produce aggregation pipelines using few-shot templates (BRD-FR-03)
- [x] Simple queries still skip NIM (regression on Sprint 2)
- [x] Syntax failures retry at most 3 times then alert the user (BRD-NFR-06)
- [x] Orchestrator never asks more than one question per turn (BRD-NFR-07)
- [x] `"just run it"` applies silent defaults
- [x] SPA shows streamed agent activity for the query path
```

- [ ] **Step 5: Run the whole suite one final time**

Run:

```bash
export PATH="$PATH:/root/.dotnet"
dotnet test MultiAgentMongoNlp.sln
```

Working directory for the SPA: `src/web`, run `npm test`.

Expected: .NET PASS (all prior 62 + sprint-3 tests); SPA PASS (3 files).

- [ ] **Step 6: Commit**

```bash
git add tests/Gateway.Tests/Nlp/Sprint3BenchTests.cs docs/benchmarks/sprint-3-nfr02.md sprints/sprint-3.md
git commit -m "Record complex MQL orchestration benchmark and tick Sprint 3 criteria"
```

---

## Notes for the executor

- Run `.NET` commands with `export PATH="$PATH:/root/.dotnet"`; the SDK is at `/root/.dotnet/dotnet` (v10.0.400). Angular commands run from `src/web`.
- `NvidiaNim:ApiKey` is `TBD` in this environment, so complex queries correctly return `ComplexLlmFailed` with an "NVIDIA NIM is not configured" style error at runtime. Once a real key is provided via env `NvidiaNim__ApiKey`, the same path produces a synthesized pipeline. Do not add a live-key test to the default suite; keep it behind a manual run.
- `NlpRouteResult` keeps `LlmTokensConsumed` for backward compatibility and adds `LlmAttempts` + `Error` at the end so existing constructions compile unchanged.
- The SSE stream only relays events published after a subscriber connects. `NlpOrchestratorTests` read the sink directly; `ApiContractTests` only assert the opening `agent.idle` frame, preserving the Sprint 1 contract.
- If `AddOpenAIChatCompletion(modelId, endpoint, apiKey)` is unavailable in the pinned SK version, use an `OpenAIClient` with `OpenAIClientOptions.Endpoint` and register the client provider; keep the `ILlmQueryGenerator` interface unchanged.
