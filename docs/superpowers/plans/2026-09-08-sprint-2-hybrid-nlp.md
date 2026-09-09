# Sprint 2 — Hybrid NLP (Cache, Slots, Simple MQL) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Route routine English listing queries through a local deterministic NLP pipeline (semantic cache → slot extraction → intent → Scriban simple-MQL) so they resolve with zero LLM tokens, using a real ONNX `bge-small` embedder.

**Architecture:** New `src/gateway/Nlp/**` components in the existing gateway project behind a JWT-protected `POST /api/nlp/query`. Pipeline: embed → cache (cosine > 0.95) → slots → intent → simple MQL or `ComplexLlmRequired`/`ClarifyRequired`. Successful simple MQL is written back into the cache. No MongoDB, no NVIDIA calls, no governance in this sprint.

**Tech Stack:** .NET 10 (`net10.0`), ASP.NET Core Minimal APIs, `Microsoft.ML.OnnxRuntime` 1.29.0 + `Microsoft.ML.Tokenizers` 1.0.3 (real `bge-small-en-v1.5` int8 model, ~34MB), `Scriban` 7.4.0, xUnit + `WebApplicationFactory` (existing `tests/Gateway.Tests`).

## Global Constraints

- Runtime floor: `net10.0` (gateway + tests) — already set by Sprint 1; do not change.
- SPA/`src/web` is untouched this sprint.
- Real ONNX embedder only — **no** deterministic stub in production code.
- Cache hit rule is strict: cosine **> 0.95** exactly (BRD-NFR-05); 0.95 itself is a miss.
- Zero LLM tokens on the cache/slot/template path; `llmTokensConsumed` is always 0 for `CacheHit`, `SimpleMql`, `ClarifyRequired`.
- No MongoDB execution, no NVIDIA endpoint call, no governance/audit writes, no AST guardrails this sprint.
- Embedding text is prefixed with the BGE query instruction `"Represent this sentence for searching relevant passages: "` (symmetric for cache write and read).
- Split latency budget (bench): in-memory cache lookup < 10 ms; slot extraction < 5 ms; simple MQL render < 2 ms; real ONNX embed is measured and recorded (~25 ms here) — NOT asserted against 10 ms.
- Assets `Nlp/Assets/**` (gazetteers + Scriban) are content-copied to the build output and resolved from `AppContext.BaseDirectory`; the ONNX model + vocab live in git-ignored `src/gateway/Models/bge-small-en-v1.5/` and are downloaded by `scripts/download-nlp-assets.sh` (config key `Nlp:Embeddings:ModelPath` / `Nlp:Embeddings:TokenizerPath`).
- Follow existing code conventions: file-scoped namespaces, top-level `Program.cs`, folder-per-feature, xUnit tests with `Using Include="Xunit"` (no `using Xunit;` needed), records for DTOs.
- Do not commit `src/web/.vscode/` or any `bin/`/`obj/`/`Models/` output. `scripts/` and asset JSON/scriban files ARE committed.

---

### Task 1: Provisioning, packages, config, and asset pipeline

**Files:**
- Create: `.gitignore` (append `Models/` line)
- Create: `scripts/download-nlp-assets.sh`
- Create: `src/gateway/Models/bge-small-en-v1.5/` (via script; git-ignored)
- Create: `src/gateway/Nlp/Assets/Gazetteers/markets.json`
- Create: `src/gateway/Nlp/Assets/Gazetteers/amenities.json`
- Create: `src/gateway/Nlp/Assets/Templates/match.scriban`
- Create: `src/gateway/Nlp/Assets/Templates/sort.scriban`
- Create: `src/gateway/Nlp/Assets/Templates/limit.scriban`
- Modify: `src/gateway/Gateway.csproj`
- Modify: `src/gateway/appsettings.json`
- Test: `tests/Gateway.Tests/Nlp/GatewayCsprojTests.cs`

**Interfaces:**
- Consumes: nothing (repo state from Sprint 1).
- Produces: committed asset files and config keys that Tasks 2+ read. `Nlp:Embeddings:ModelPath` default = `Models/bge-small-en-v1.5/model_quantized.onnx`; `Nlp:Embeddings:TokenizerPath` default = `Models/bge-small-en-v1.5/vocab.txt`.

- [ ] **Step 1: Add gitignore + config + package references**

Append to `.gitignore`:

```gitignore
src/gateway/Models/
```

Update `src/gateway/appsettings.json` — append a top-level `Nlp` section (keep existing `Jwt`, `MongoDb`, `NvidiaNim` sections):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Jwt": {
    "Issuer": "MultiAgentMongoNlp",
    "Audience": "MultiAgentMongoNlp.Spa",
    "SigningKey": "DEV-ONLY-CHANGE-ME-32CHARS-MINIMUM!!"
  },
  "MongoDb": {
    "ConnectionString": "mongodb://localhost:27017",
    "Database": "sample_airbnb"
  },
  "NvidiaNim": {
    "BaseUrl": "https://integrate.api.nvidia.com/v1",
    "ApiKey": "",
    "Model": ""
  },
  "Nlp": {
    "Embeddings": {
      "ModelPath": "Models/bge-small-en-v1.5/model_quantized.onnx",
      "TokenizerPath": "Models/bge-small-en-v1.5/vocab.txt",
      "MaxTokens": 512
    },
    "Cache": {
      "Threshold": 0.95
    },
    "Defaults": {
      "Limit": 10,
      "Sort": "rating_desc",
      "Market": "All"
    }
  }
}
```

Update `src/gateway/Gateway.csproj` to add packages and content-copy the assets:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="Gateway.Tests" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="FluentValidation.AspNetCore" Version="11.3.0" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.11" />
    <PackageReference Include="Microsoft.ML.OnnxRuntime" Version="1.29.0" />
    <PackageReference Include="Microsoft.ML.Tokenizers" Version="1.0.3" />
    <PackageReference Include="Scriban" Version="7.4.0" />
    <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.22.0" />
  </ItemGroup>

  <ItemGroup>
    <Content Include="Nlp/Assets/**/*.json;Nlp/Assets/**/*.scriban">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
    </Content>
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Create download script**

Create `scripts/download-nlp-assets.sh` (executable):

```bash
#!/usr/bin/env bash
# Downloads the bge-small-en-v1.5 ONNX model + tokenizer vocab into src/gateway/Models
# (git-ignored). Run once before first `dotnet run` or `dotnet test`.

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEST="$ROOT/src/gateway/Models/bge-small-en-v1.5"
BASE="https://huggingface.co/Xenova/bge-small-en-v1.5/resolve/main"

mkdir -p "$DEST"

download() {
  local file="$1"
  if [ ! -f "$DEST/$file" ]; then
    echo "Downloading $file ..."
    curl -sL --fail -o "$DEST/$file" "$BASE/$file"
  else
    echo "$file already present"
  fi
}

download "onnx/model_quantized.onnx"
download "vocab.txt"

echo "NLP model assets ready at $DEST"
```

Make it executable and run it:

```bash
chmod +x scripts/download-nlp-assets.sh
./scripts/download-nlp-assets.sh
```

Expected: prints download lines, leaves `model_quantized.onnx` (~34MB) and `vocab.txt` under `src/gateway/Models/bge-small-en-v1.5/`.

- [ ] **Step 3: Create asset files**

`src/gateway/Nlp/Assets/Gazetteers/markets.json`:

```json
{
  "markets": [
    { "canonical": "Los Angeles", "aliases": ["los angeles", "la", "l.a.", "la california", "los angeles california"] },
    { "canonical": "New York", "aliases": ["new york", "nyc", "ny", "new york city", "manhattan"] },
    { "canonical": "San Francisco", "aliases": ["san francisco", "sf", "bay area"] },
    { "canonical": "Miami", "aliases": ["miami", "miami beach"] },
    { "canonical": "Chicago", "aliases": ["chicago"] },
    { "canonical": "Austin", "aliases": ["austin", "austin texas"] },
    { "canonical": "Seattle", "aliases": ["seattle"] }
  ]
}
```

`src/gateway/Nlp/Assets/Gazetteers/amenities.json`:

```json
{
  "amenities": [
    { "canonical": "Pool", "aliases": ["pool", "pools", "swimming pool", "swimming pools", "heated pool"] },
    { "canonical": "Wireless Internet", "aliases": ["wifi", "wi-fi", "wireless internet", "internet"] },
    { "canonical": "Free parking on premises", "aliases": ["parking", "free parking", "parking on premises"] },
    { "canonical": "Kitchen", "aliases": ["kitchen", "full kitchen"] },
    { "canonical": "Washer", "aliases": ["washer", "washing machine"] },
    { "canonical": "Dryer", "aliases": ["dryer"] },
    { "canonical": "Hot tub", "aliases": ["hot tub", "jacuzzi"] },
    { "canonical": "Gym", "aliases": ["gym", "fitness center", "home gym"] }
  ]
}
```

`src/gateway/Nlp/Assets/Templates/match.scriban`:

```text
{{ if has_conditions -}}
{ "$match": { {{ for c in conditions }}{{ c }}{{ if !for.last }}, {{ end }}{{ end }} } }
{{- end }}
```

`src/gateway/Nlp/Assets/Templates/sort.scriban`:

```text
{ "$sort": { {{ sort_field }}: {{ sort_direction }} } }
```

`src/gateway/Nlp/Assets/Templates/limit.scriban`:

```text
{ "$limit": {{ limit }} }
```

- [ ] **Step 4: Write test verifying packaged assets exist in output**

Create `tests/Gateway.Tests/Nlp/GatewayCsprojTests.cs`:

```csharp
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
    }
}
```

- [ ] **Step 5: Run tests to verify they pass (assets packaged)**

Run: `cd /workspace && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~GatewayCsprojTests"`
Expected: 1 test PASS.

- [ ] **Step 6: Commit**

```bash
cd /workspace
git add .gitignore scripts/download-nlp-assets.sh src/gateway/Nlp/Assets src/gateway/Gateway.csproj src/gateway/appsettings.json tests/Gateway.Tests/Nlp/GatewayCsprojTests.cs
git commit -m "feat: provision Sprint 2 NLP assets, packages, and config"
```

---

### Task 2: Cosine similarity and text embedding contract

**Files:**
- Create: `src/gateway/Nlp/Abstractions/ITextEmbedder.cs`
- Create: `src/gateway/Nlp/Cache/CosineSimilarity.cs`
- Test: `tests/Gateway.Tests/Nlp/CosineSimilarityTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces:
  - `Gateway.Nlp.Abstractions.ITextEmbedder` — `float[] Embed(string text)`.
  - `Gateway.Nlp.Cache.CosineSimilarity` — `static double Cosine(float[] a, float[] b)`, `static bool IsAbove(float[] a, float[] b, double threshold)`.
  - `Gateway.Nlp.Cache.CacheDefaults` — `public const double Threshold = 0.95;`

- [ ] **Step 1: Write the failing tests**

Create `tests/Gateway.Tests/Nlp/CosineSimilarityTests.cs`:

```csharp
using Gateway.Nlp.Cache;

namespace Gateway.Tests.Nlp;

public class CosineSimilarityTests
{
    [Fact]
    public void Identical_vectors_score_one()
    {
        var v = new float[] { 1f, 0f, 0f };

        Assert.Equal(1.0, CosineSimilarity.Cosine(v, v), 6);
    }

    [Fact]
    public void Orthogonal_vectors_score_zero()
    {
        var a = new float[] { 1f, 0f, 0f };
        var b = new float[] { 0f, 1f, 0f };

        Assert.Equal(0.0, CosineSimilarity.Cosine(a, b), 6);
    }

    [Theory]
    [InlineData(0.951, true)]
    [InlineData(0.95, false)]
    [InlineData(0.5, false)]
    [InlineData(1.0, true)]
    public void Above_threshold_is_strictly_greater(double cosine, bool expected)
    {
        var a = new float[] { (float)cosine, (float)Math.Sqrt(1 - cosine * cosine), 0f };
        var b = new float[] { 1f, 0f, 0f };

        Assert.Equal(expected, CosineSimilarity.IsAbove(a, b, CacheDefaults.Threshold));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~CosineSimilarityTests"`
Expected: FAIL — type `CosineSimilarity` does not exist.

- [ ] **Step 3: Implement**

Create `src/gateway/Nlp/Cache/CosineSimilarity.cs`:

```csharp
namespace Gateway.Nlp.Cache;

public static class CacheDefaults
{
    public const double Threshold = 0.95;
}

public static class CosineSimilarity
{
    public static double Cosine(float[] a, float[] b)
    {
        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += (double)a[i] * b[i];
            normA += (double)a[i] * a[i];
            normB += (double)b[i] * b[i];
        }

        if (normA == 0 || normB == 0) return 0;
        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    public static bool IsAbove(float[] a, float[] b, double threshold)
    {
        return Cosine(a, b) > threshold;
    }
}
```

Create `src/gateway/Nlp/Abstractions/ITextEmbedder.cs`:

```csharp
namespace Gateway.Nlp.Abstractions;

public interface ITextEmbedder
{
    float[] Embed(string text);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~CosineSimilarityTests"`
Expected: all PASS (the theory rows are in exact ascending order; verify 0.951 passes).

- [ ] **Step 5: Commit**

```bash
cd /workspace
git add src/gateway/Nlp/Abstractions/ITextEmbedder.cs src/gateway/Nlp/Cache/CosineSimilarity.cs tests/Gateway.Tests/Nlp/CosineSimilarityTests.cs
git commit -m "feat: add cosine similarity and embedding contract"
```

---

### Task 3: Real ONNX bge-small embedder

**Files:**
- Create: `src/gateway/Nlp/Embeddings/OnnxBgeSmallEmbedder.cs`
- Test: `tests/Gateway.Tests/Nlp/OnnxBgeSmallEmbedderTests.cs`
- Test: `tests/Gateway.Tests/Nlp/TestPaths.cs` (helper)

**Interfaces:**
- Consumes: `ITextEmbedder`, Task 1 config asset defaults, downloaded model/vocab.
- Produces: `Gateway.Nlp.Embeddings.OnnxBgeSmallEmbedder` implementing `ITextEmbedder`; ctor `(string modelPath, string vocabPath, int maxTokens = 512)`; prepends BGE instruction `const string Instruction = "Represent this sentence for searching relevant passages: "`; returns L2-normalized float[384].
- Also produces `Gateway.Nlp.Embeddings.EmbedderDimensions` — `public const int Dimension = 384;`.

- [ ] **Step 1: Write the failing tests**

Create `tests/Gateway.Tests/Nlp/TestPaths.cs`:

```csharp
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
```

Create `tests/Gateway.Tests/Nlp/OnnxBgeSmallEmbedderTests.cs`:

```csharp
using Gateway.Nlp.Cache;
using Gateway.Nlp.Embeddings;

namespace Gateway.Tests.Nlp;

public class OnnxBgeSmallEmbedderTests
{
    private readonly OnnxBgeSmallEmbedder _embedder;

    public OnnxBgeSmallEmbedderTests()
    {
        Assert.True(File.Exists(TestPaths.ModelPath()), "Run scripts/download-nlp-assets.sh first.");
        Assert.True(File.Exists(TestPaths.VocabPath()), "Run scripts/download-nlp-assets.sh first.");
        _embedder = new OnnxBgeSmallEmbedder(TestPaths.ModelPath(), TestPaths.VocabPath());
    }

    [Fact]
    public void Returns_384_dimension_l2_normalized_vector()
    {
        var v = _embedder.Embed("listings with pools in Los Angeles");

        Assert.Equal(EmbedderDimensions.Dimension, v.Length);
        var norm = Math.Sqrt(v.Sum(x => (double)x * x));
        Assert.Equal(1.0, norm, 4);
    }

    [Fact]
    public void Identical_text_scores_cosine_one()
    {
        var a = _embedder.Embed("listings with pools in Los Angeles");
        var b = _embedder.Embed("listings with pools in Los Angeles");

        Assert.True(CosineSimilarity.IsAbove(a, b, 0.999));
    }

    [Fact]
    public void Near_paraphrase_scores_above_095()
    {
        var a = _embedder.Embed("listings with pools in Los Angeles");
        var b = _embedder.Embed("listings that have pools in Los Angeles");

        Assert.True(CosineSimilarity.IsAbove(a, b, 0.95));
    }

    [Fact]
    public void Distinct_query_scores_well_below_095()
    {
        var a = _embedder.Embed("listings with pools in Los Angeles");
        var b = _embedder.Embed("luxury condos in New York under 500");

        Assert.False(CosineSimilarity.IsAbove(a, b, 0.95));
        Assert.True(CosineSimilarity.Cosine(a, b) < 0.8);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~OnnxBgeSmallEmbedderTests"`
Expected: FAIL — type does not exist. (Model present; skip-mark not needed — fail-fast is the intended contract.)

- [ ] **Step 3: Implement the embedder**

Create `src/gateway/Nlp/Embeddings/OnnxBgeSmallEmbedder.cs`:

```csharp
using System.Buffers;
using Gateway.Nlp.Abstractions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace Gateway.Nlp.Embeddings;

public static class EmbedderDimensions
{
    public const int Dimension = 384;
}

public sealed class OnnxBgeSmallEmbedder : ITextEmbedder, IDisposable
{
    public const string Instruction = "Represent this sentence for searching relevant passages: ";

    private readonly InferenceSession _session;
    private readonly BertTokenizer _tokenizer;
    private readonly int _maxTokens;

    public OnnxBgeSmallEmbedder(string modelPath, string vocabPath, int maxTokens = 512)
    {
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException(
                $"ONNX model not found at '{modelPath}'. Run scripts/download-nlp-assets.sh first.", modelPath);
        }

        if (!File.Exists(vocabPath))
        {
            throw new FileNotFoundException(
                $"Tokenizer vocab not found at '{vocabPath}'. Run scripts/download-nlp-assets.sh first.", vocabPath);
        }

        _session = new InferenceSession(modelPath);
        _tokenizer = BertTokenizer.Create(vocabPath);
        _maxTokens = maxTokens;
    }

    public float[] Embed(string text)
    {
        var tokens = _tokenizer.EncodeToIds(Instruction + text);
        var count = Math.Min(tokens.Count, _maxTokens);

        var ids = new long[count];
        for (var i = 0; i < count; i++) ids[i] = tokens[i];

        var mask = new long[count];
        var types = new long[count];
        Array.Fill(mask, 1L);

        var inputIds = new DenseTensor<long>(ids, new[] { 1, count });
        var attention = new DenseTensor<long>(mask, new[] { 1, count });
        var typeIds = new DenseTensor<long>(types, new[] { 1, count });

        using var results = _session.Run(new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attention),
            NamedOnnxValue.CreateFromTensor("token_type_ids", typeIds)
        });

        var output = results.First(r => r.Name == "last_hidden_state").AsTensor<float>();
        var vector = new float[EmbedderDimensions.Dimension];
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = output[0, 0, i];
        }

        var norm = 0.0;
        foreach (var x in vector) norm += (double)x * x;
        norm = Math.Sqrt(norm);
        if (norm > 0)
        {
            for (var i = 0; i < vector.Length; i++) vector[i] = (float)(vector[i] / norm);
        }

        return vector;
    }

    public void Dispose()
    {
        _session.Dispose();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~OnnxBgeSmallEmbedderTests"`
Expected: all 4 PASS (matches measured cosines: near-paraphrase ≈ 0.986; distinct ≈ 0.59).

- [ ] **Step 5: Commit**

```bash
cd /workspace
git add src/gateway/Nlp/Embeddings/OnnxBgeSmallEmbedder.cs tests/Gateway.Tests/Nlp/OnnxBgeSmallEmbedderTests.cs tests/Gateway.Tests/Nlp/TestPaths.cs
git commit -m "feat: add real ONNX bge-small embedder"
```

---

### Task 4: In-memory semantic cache

**Files:**
- Create: `src/gateway/Nlp/Cache/ISemanticCache.cs`
- Create: `src/gateway/Nlp/Cache/SemanticCache.cs`
- Test: `tests/Gateway.Tests/Nlp/SemanticCacheTests.cs`

**Interfaces:**
- Consumes: `CosineSimilarity`, `CacheDefaults`, `ITextEmbedder` (not directly; cache works on precomputed vectors).
- Produces:
  - `Gateway.Nlp.Cache.CacheEntry` — record `(float[] Vector, string Mql, string CanonicalQuery)`.
  - `Gateway.Nlp.Cache.ISemanticCache` — `CacheEntry? TryFind(float[] queryVector); void Store(float[] queryVector, string canonicalQuery, string mql);`
  - `Gateway.Nlp.Cache.SemanticCache` — thread-safe `ConcurrentDictionary`-backed linear scan; store replaces matching entry if a higher-cosine vector existed.

- [ ] **Step 1: Write the failing tests**

Create `tests/Gateway.Tests/Nlp/SemanticCacheTests.cs`:

```csharp
using Gateway.Nlp.Cache;

namespace Gateway.Tests.Nlp;

public class SemanticCacheTests
{
    private static float[] Unit(params double[] xs)
    {
        var v = xs.Select(x => (float)x).ToArray();
        var norm = Math.Sqrt(v.Sum(x => (double)x * x));
        return v.Select(x => (float)(x / norm)).ToArray();
    }

    [Fact]
    public void Miss_on_empty_cache()
    {
        var cache = new SemanticCache();
        Assert.Null(cache.TryFind(Unit(1, 0, 0)));
    }

    [Fact]
    public void Exact_repeat_is_a_hit()
    {
        var cache = new SemanticCache();
        cache.Store(Unit(1, 0, 0), "q", "[{\"$limit\":10}]");

        var hit = cache.TryFind(Unit(1, 0, 0));

        Assert.NotNull(hit);
        Assert.Equal("[{\"$limit\":10}]", hit!.Mql);
        Assert.Equal("q", hit.CanonicalQuery);
    }

    [Fact]
    public void Below_threshold_is_a_miss()
    {
        var cache = new SemanticCache();
        cache.Store(Unit(1, 0, 0), "q", "[{\"$limit\":10}]");

        Assert.Null(cache.TryFind(Unit(0.8, 0.6, 0)));
    }

    [Fact]
    public void Above_threshold_is_a_hit()
    {
        var cache = new SemanticCache();
        cache.Store(Unit(1, 0, 0), "q", "[{\"$limit\":10}]");

        var hit = cache.TryFind(Unit(0.96, 0.28, 0));

        Assert.NotNull(hit);
    }

    [Fact]
    public async Task Concurrent_stores_and_finds_do_not_throw()
    {
        var cache = new SemanticCache();
        var tasks = Enumerable.Range(0, 32)
            .Select(i => Task.Run(() =>
            {
                cache.Store(Unit(i + 1, 1, 0), $"q{i}", "[{\"$limit\":10}]");
                _ = cache.TryFind(Unit(i + 1, 1, 0));
            }));

        await Task.WhenAll(tasks);
        Assert.NotNull(cache.TryFind(Unit(5, 1, 0)));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~SemanticCacheTests"`
Expected: FAIL — types not defined.

- [ ] **Step 3: Implement**

Create `src/gateway/Nlp/Cache/ISemanticCache.cs`:

```csharp
namespace Gateway.Nlp.Cache;

public sealed record CacheEntry(float[] Vector, string Mql, string CanonicalQuery);

public interface ISemanticCache
{
    CacheEntry? TryFind(float[] queryVector);
    void Store(float[] queryVector, string canonicalQuery, string mql);
}
```

Create `src/gateway/Nlp/Cache/SemanticCache.cs`:

```csharp
namespace Gateway.Nlp.Cache;

public sealed class SemanticCache : ISemanticCache
{
    private readonly object _gate = new();
    private readonly List<CacheEntry> _entries = new();
    private readonly double _threshold = CacheDefaults.Threshold;

    public CacheEntry? TryFind(float[] queryVector)
    {
        lock (_gate)
        {
            CacheEntry? best = null;
            var bestScore = _threshold;
            foreach (var entry in _entries)
            {
                var score = CosineSimilarity.Cosine(queryVector, entry.Vector);
                if (score > bestScore)
                {
                    best = entry;
                    bestScore = score;
                }
            }

            return best;
        }
    }

    public void Store(float[] queryVector, string canonicalQuery, string mql)
    {
        lock (_gate)
        {
            CacheEntry? best = null;
            var bestScore = _threshold;
            foreach (var entry in _entries)
            {
                var score = CosineSimilarity.Cosine(queryVector, entry.Vector);
                if (score > bestScore)
                {
                    best = entry;
                    bestScore = score;
                }
            }

            if (best is not null)
            {
                _entries.Remove(best);
            }

            _entries.Add(new CacheEntry(queryVector, mql, canonicalQuery));
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~SemanticCacheTests"`
Expected: all 5 PASS.

- [ ] **Step 5: Commit**

```bash
cd /workspace
git add src/gateway/Nlp/Cache/ISemanticCache.cs src/gateway/Nlp/Cache/SemanticCache.cs tests/Gateway.Tests/Nlp/SemanticCacheTests.cs
git commit -m "feat: add in-memory semantic cache with cosine gate"
```

---

### Task 5: Gazetteer and slot extraction

**Files:**
- Create: `src/gateway/Nlp/Slots/Gazetteer.cs`
- Create: `src/gateway/Nlp/Slots/ExtractedSlots.cs`
- Create: `src/gateway/Nlp/Slots/SlotExtractor.cs`
- Test: `tests/Gateway.Tests/Nlp/SlotExtractorTests.cs`

**Interfaces:**
- Consumes: Task 1 gazetteer JSON assets; `System.Text.Json`.
- Produces:
  - `Gateway.Nlp.Slots.Gazetteer` — record/class holding market + amenity alias maps; static `Gazetteer LoadFromDirectory(string assetRoot)` reading `Nlp/Assets/Gazetteers/{markets,amenities}.json`.
  - `Gateway.Nlp.Slots.ExtractedSlots` — record `(string? Market, int? MinBedrooms, int? MinBeds, decimal? MinPrice, decimal? MaxPrice, IReadOnlyList<string> Amenities, bool JustRunIt)`.
  - `Gateway.Nlp.Slots.SlotExtractor` — `ExtractedSlots Extract(string utterance, Gazetteer gazetteer)`.
- Matching is case-insensitive over the lowercased utterance. Market via alias longest-match; price via regex `under \$(\d+)`, `over \$(\d+)`, `\$(\d+)-?\$?(\d*)`, `between \$?(\d+) and \$?(\d+)`; beds via `(\d+)\s*(bed|bedroom)s?`; just-run-it phrase tokens.

- [ ] **Step 1: Write the failing tests**

Create `tests/Gateway.Tests/Nlp/SlotExtractorTests.cs`:

```csharp
using Gateway.Nlp.Slots;

namespace Gateway.Tests.Nlp;

public class SlotExtractorTests
{
    private static readonly Gazetteer Gazetteer = Gazetteer.LoadFromDirectory(
        Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Gazetteers"));

    [Fact]
    public void Extracts_market_from_city_name()
    {
        var slots = SlotExtractor.Extract("listings in Los Angeles", Gazetteer);
        Assert.Equal("Los Angeles", slots.Market);
    }

    [Fact]
    public void Extracts_market_from_alias()
    {
        var slots = SlotExtractor.Extract("places in LA", Gazetteer);
        Assert.Equal("Los Angeles", slots.Market);
    }

    [Fact]
    public void Extracts_price_under_bound()
    {
        var slots = SlotExtractor.Extract("homes under $200", Gazetteer);
        Assert.Equal(200m, slots.MaxPrice);
        Assert.Null(slots.MinPrice);
    }

    [Fact]
    public void Extracts_price_range()
    {
        var slots = SlotExtractor.Extract("homes between 150 and 250", Gazetteer);
        Assert.Equal(150m, slots.MinPrice);
        Assert.Equal(250m, slots.MaxPrice);
    }

    [Fact]
    public void Extracts_bedrooms()
    {
        var slots = SlotExtractor.Extract("2 bedroom apartments", Gazetteer);
        Assert.Equal(2, slots.MinBedrooms);
    }

    [Fact]
    public void Extracts_amenity_via_alias()
    {
        var slots = SlotExtractor.Extract("apartments with pools", Gazetteer);
        Assert.Contains("Pool", slots.Amenities);
    }

    [Fact]
    public void Detects_just_run_it()
    {
        var slots = SlotExtractor.Extract("listings with pools in Los Angeles, just run it", Gazetteer);
        Assert.True(slots.JustRunIt);
        Assert.Equal("Los Angeles", slots.Market);
        Assert.Contains("Pool", slots.Amenities);
    }

    [Fact]
    public void Returns_empty_for_generic_browse()
    {
        var slots = SlotExtractor.Extract("show me top listings", Gazetteer);
        Assert.Null(slots.Market);
        Assert.Null(slots.MaxPrice);
        Assert.Empty(slots.Amenities);
        Assert.False(slots.JustRunIt);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~SlotExtractorTests"`
Expected: FAIL — types not defined.

- [ ] **Step 3: Implement**

Create `src/gateway/Nlp/Slots/ExtractedSlots.cs`:

```csharp
namespace Gateway.Nlp.Slots;

public sealed record ExtractedSlots(
    string? Market,
    int? MinBedrooms,
    int? MinBeds,
    decimal? MinPrice,
    decimal? MaxPrice,
    IReadOnlyList<string> Amenities,
    bool JustRunIt)
{
    public static readonly ExtractedSlots Empty = new(null, null, null, null, null, Array.Empty<string>(), false);

    public bool HasAnyConstraints => Market is not null || MinBedrooms is not null || MinBeds is not null ||
                                     MinPrice is not null || MaxPrice is not null || Amenities.Count > 0;
}
```

Create `src/gateway/Nlp/Slots/Gazetteer.cs`:

```csharp
using System.Text.Json;

namespace Gateway.Nlp.Slots;

public sealed class Gazetteer
{
    private Gazetteer(
        IReadOnlyDictionary<string, string> marketAliases,
        IReadOnlyDictionary<string, string> amenityAliases)
    {
        MarketAliases = marketAliases;
        AmenityAliases = amenityAliases;
    }

    public IReadOnlyDictionary<string, string> MarketAliases { get; }
    public IReadOnlyDictionary<string, string> AmenityAliases { get; }

    public static Gazetteer LoadFromDirectory(string directory)
    {
        var markets = JsonSerializer.Deserialize<GazetteerFile>(
            File.ReadAllText(Path.Combine(directory, "markets.json")))!;
        var amenities = JsonSerializer.Deserialize<GazetteerFile>(
            File.ReadAllText(Path.Combine(directory, "amenities.json")))!;

        return new Gazetteer(BuildAliases(markets), BuildAliases(amenities));
    }

    private static Dictionary<string, string> BuildAliases(GazetteerFile file)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in file.Markets.Concat(file.Amenities))
        {
            foreach (var alias in entry.Aliases.Append(entry.Canonical.ToLowerInvariant()))
            {
                map[alias] = entry.Canonical;
            }
        }

        return map;
    }

    private sealed class GazetteerFile
    {
        public List<GazetteerEntry> Markets { get; set; } = new();
        public List<GazetteerEntry> Amenities { get; set; } = new();
    }

    private sealed class GazetteerEntry
    {
        public string Canonical { get; set; } = "";
        public List<string> Aliases { get; set; } = new();
    }
}
```

Wait — the JSON files use key `markets`/`amenities` for both files, so a single `GazetteerEntry` with lists is fine; but `markets.json` file has `{ "markets": [...] }` while `amenities.json` has `{ "amenities": [...] }`. The `GazetteerFile` deserializer reads both keys into `Markets` and `Amenities`, so loading `markets.json` fills only `Markets` and `amenities.json` fills only `Amenities`. `BuildAliases` must therefore iterate the non-empty list only. Fix `BuildAliases` to accept a single `List<GazetteerEntry>`:

```csharp
using System.Text.Json;

namespace Gateway.Nlp.Slots;

public sealed class Gazetteer
{
    private Gazetteer(
        IReadOnlyDictionary<string, string> marketAliases,
        IReadOnlyDictionary<string, string> amenityAliases)
    {
        MarketAliases = marketAliases;
        AmenityAliases = amenityAliases;
    }

    public IReadOnlyDictionary<string, string> MarketAliases { get; }
    public IReadOnlyDictionary<string, string> AmenityAliases { get; }

    public static Gazetteer LoadFromDirectory(string directory)
    {
        var markets = Load(Path.Combine(directory, "markets.json"));
        var amenities = Load(Path.Combine(directory, "amenities.json"));
        return new Gazetteer(BuildAliases(markets), BuildAliases(amenities));
    }

    private static List<GazetteerEntry> Load(string path)
    {
        var doc = JsonDocument.Parse(File.ReadAllText(path));
        var prop = doc.RootElement.EnumerateObject().First();
        return prop.Value.EnumerateArray()
            .Select(e => new GazetteerEntry
            {
                Canonical = e.GetProperty("canonical").GetString()!,
                Aliases = e.GetProperty("aliases").EnumerateArray().Select(a => a.GetString()!).ToList()
            })
            .ToList();
    }

    private static Dictionary<string, string> BuildAliases(List<GazetteerEntry> entries)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            foreach (var alias in entry.Aliases.Append(entry.Canonical.ToLowerInvariant()))
            {
                map[alias] = entry.Canonical;
            }
        }

        return map;
    }

    private sealed class GazetteerEntry
    {
        public string Canonical { get; set; } = "";
        public List<string> Aliases { get; set; } = new();
    }
}
```

- [ ] **Step 3b: Implement the SlotExtractor**

Create `src/gateway/Nlp/Slots/SlotExtractor.cs`:

```csharp
using System.Text.RegularExpressions;

namespace Gateway.Nlp.Slots;

public static class SlotExtractor
{
    private static readonly Regex PriceUnder = new(@"under\s+\$?\s*(\d+(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PriceOver = new(@"over\s+\$?\s*(\d+(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PriceRange = new(@"\$?\s*(\d+(?:\.\d+)?)\s*[-–]\s*\$?\s*(\d+(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PriceBetween = new(@"between\s+\$?\s*(\d+(?:\.\d+)?)\s+and\s+\$?\s*(\d+(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Beds = new(@"(\d+)\s*(?:bed|bedroom)s?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static ExtractedSlots Extract(string utterance, Gazetteer gazetteer)
    {
        var lower = utterance.ToLowerInvariant();
        var amenities = gazetteer.AmenityAliases
            .Where(kvp => lower.Contains(kvp.Key))
            .Select(kvp => kvp.Value)
            .Distinct()
            .ToList();

        string? market = null;
        var marketCandidates = gazetteer.MarketAliases
            .Where(kvp => lower.Contains(kvp.Key))
            .OrderByDescending(kvp => kvp.Key.Length)
            .ToList();
        if (marketCandidates.Count > 0)
        {
            market = marketCandidates[0].Value;
        }

        var between = PriceBetween.Match(lower);
        var range = PriceRange.Match(lower);
        var under = PriceUnder.Match(lower);
        var over = PriceOver.Match(lower);
        var beds = Beds.Match(lower);

        decimal? minPrice = null, maxPrice = null;
        int? bedrooms = null, minBeds = null;

        if (between.Success)
        {
            minPrice = ParsePrice(between.Groups[1].Value);
            maxPrice = ParsePrice(between.Groups[2].Value);
        }
        else if (range.Success)
        {
            minPrice = ParsePrice(range.Groups[1].Value);
            maxPrice = ParsePrice(range.Groups[2].Value);
        }
        else if (under.Success)
        {
            maxPrice = ParsePrice(under.Groups[1].Value);
        }
        else if (over.Success)
        {
            minPrice = ParsePrice(over.Groups[1].Value);
        }

        if (beds.Success)
        {
            var value = int.Parse(beds.Groups[1].Value);
            var isBedrooms = beds.Groups[2].Value.StartsWith("bed", StringComparison.OrdinalIgnoreCase) &&
                             beds.Groups[2].Value.Contains("bedroom", StringComparison.OrdinalIgnoreCase);
            if (lower.Contains("bedroom"))
            {
                bedrooms = value;
            }
            else
            {
                minBeds = value;
            }
        }

        var justRunIt = lower.Contains("just run it") || lower.Contains("go ahead") || lower.Contains("run it");

        return new ExtractedSlots(market, bedrooms, minBeds, minPrice, maxPrice, amenities, justRunIt);
    }

    private static decimal ParsePrice(string raw)
    {
        return decimal.TryParse(raw, out var value) ? value : 0m;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~SlotExtractorTests"`
Expected: all 8 PASS. If the range regex or price tests fail, adjust regex ordering (between must be tested before the generic `$150-$250` range).

- [ ] **Step 5: Commit**

```bash
cd /workspace
git add src/gateway/Nlp/Slots tests/Gateway.Tests/Nlp/SlotExtractorTests.cs
git commit -m "feat: add gazetteer-backed slot extraction"
```

---

### Task 6: Intent classifier

**Files:**
- Create: `src/gateway/Nlp/Intent/IntentKind.cs`
- Create: `src/gateway/Nlp/Intent/IntentClassifier.cs`
- Test: `tests/Gateway.Tests/Nlp/IntentClassifierTests.cs`

**Interfaces:**
- Consumes: nothing (pure rule engine).
- Produces:
  - `Gateway.Nlp.Intent.IntentKind` — `enum { Search, Export, Clarify }`.
  - `Gateway.Nlp.Intent.IntentResult` — record `(IntentKind Kind, bool IsComplex, string? ClarificationQuestion)`.
  - `Gateway.Nlp.Intent.IntentClassifier` — `IntentResult Classify(string utterance)`.

Rules (in priority order):
1. `Clarify` when no search/export verb and utterance looks like a question/greeting (`what can you`, `how do`, `hi`, `hello`, `help`, ends with `?` and contains no listing noun).
2. `Export` when contains `export`, `download`, `csv`, `xlsx`, `send me a file`.
3. `Search` otherwise, with `IsComplex = true` when utterance contains aggregation/comparison signals (`average`, `avg`, `median`, `total`, `count of`, `compare`, `trend`, `season`, `by market`, `group`, `ranking`, `coziest`, `most expensive`, `cheapest`, `highest`, `lowest`, `near `).

- [ ] **Step 1: Write the failing tests**

Create `tests/Gateway.Tests/Nlp/IntentClassifierTests.cs`:

```csharp
using Gateway.Nlp.Intent;

namespace Gateway.Tests.Nlp;

public class IntentClassifierTests
{
    private readonly IntentClassifier _classifier = new();

    [Theory]
    [InlineData("show listings with pools in Los Angeles", IntentKind.Search, false)]
    [InlineData("find 2 bedroom apartments under 200", IntentKind.Search, false)]
    [InlineData("just run it", IntentKind.Search, false)]
    [InlineData("export the results to csv", IntentKind.Export, false)]
    [InlineData("download listings as xlsx", IntentKind.Export, false)]
    [InlineData("what can you do?", IntentKind.Clarify, false)]
    [InlineData("hi", IntentKind.Clarify, false)]
    [InlineData("average price by market", IntentKind.Search, true)]
    [InlineData("coziest neighborhoods near the beach by season", IntentKind.Search, true)]
    public void Classifies_utterance(string utterance, IntentKind expectedKind, bool expectedComplex)
    {
        var result = _classifier.Classify(utterance);

        Assert.Equal(expectedKind, result.Kind);
        Assert.Equal(expectedComplex, result.IsComplex);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~IntentClassifierTests"`
Expected: FAIL — types not defined.

- [ ] **Step 3: Implement**

Create `src/gateway/Nlp/Intent/IntentKind.cs`:

```csharp
namespace Gateway.Nlp.Intent;

public enum IntentKind
{
    Search,
    Export,
    Clarify
}

public sealed record IntentResult(IntentKind Kind, bool IsComplex, string? ClarificationQuestion);
```

Create `src/gateway/Nlp/Intent/IntentClassifier.cs`:

```csharp
namespace Gateway.Nlp.Intent;

public sealed class IntentClassifier
{
    private static readonly string[] ExportSignals =
        ["export", "download", "csv", "xlsx", "send me a file"];

    private static readonly string[] ComplexSignals =
        ["average", "avg", "median", "total", "count of", "compare", "trend", "season",
         "by market", "group", "ranking", "coziest", "most expensive", "cheapest",
         "highest", "lowest", "near "];

    private static readonly string[] ClarifySignals =
        ["what can you", "how do you", "what do you", "help", "hi", "hello", "hey"];

    public IntentResult Classify(string utterance)
    {
        var lower = utterance.ToLowerInvariant();
        var trimmed = lower.Trim();

        var isExport = ExportSignals.Any(lower.Contains);
        var isComplex = ComplexSignals.Any(lower.Contains);
        var isClarifyGreeting = ClarifySignals.Any(lower.Contains) &&
                                !IsListingNounPresent(lower) &&
                                !lower.Contains(" in ");

        if (trimmed is "hi" or "hello" or "hey" or "help")
        {
            return new IntentResult(IntentKind.Clarify, false, "What would you like to do? I can find listings, filter by price or market, or export results.");
        }

        if (isClarifyGreeting)
        {
            return new IntentResult(IntentKind.Clarify, false, "What would you like to do? I can find listings, filter by price or market, or export results.");
        }

        if (isExport)
        {
            return new IntentResult(IntentKind.Export, false, null);
        }

        return new IntentResult(IntentKind.Search, isComplex, null);
    }

    private static bool IsListingNounPresent(string lower)
    {
        return lower.Contains("listing") || lower.Contains("home") || lower.Contains("place") ||
               lower.Contains("apartment") || lower.Contains("property") || lower.Contains("house") ||
               lower.Contains("stay") || lower.Contains("market");
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~IntentClassifierTests"`
Expected: all theory rows PASS. Adjust Clarify heuristics if "what can you do?" leaks to Search (it contains no listing noun, so should classify Clarify).

- [ ] **Step 5: Commit**

```bash
cd /workspace
git add src/gateway/Nlp/Intent tests/Gateway.Tests/Nlp/IntentClassifierTests.cs
git commit -m "feat: add rule-based intent classifier"
```

---

### Task 7: Scriban simple MQL builder

**Files:**
- Create: `src/gateway/Nlp/Mql/IMqlBuilder.cs`
- Create: `src/gateway/Nlp/Mql/ScribanSimpleMqlBuilder.cs`
- Test: `tests/Gateway.Tests/Nlp/ScribanSimpleMqlBuilderTests.cs`

**Interfaces:**
- Consumes: `ExtractedSlots`, Task 1 Scriban templates; `Scriban.Template` rendering of `match.scriban`, `sort.scriban`, `limit.scriban`.
- Produces:
  - `Gateway.Nlp.Mql.IMqlBuilder` — `string Build(ExtractedSlots slots, MqlDefaults defaults)`.
  - `Gateway.Nlp.Mql.MqlDefaults` — record `(int Limit, string Sort, string Market)`, with `public static readonly MqlDefaults Standard = new(10, "rating_desc", "All");`
  - Output is a JSON array string, e.g. `[{"$sort":{"review_scores.rating":-1}},{"$limit":10}]` (no `$match` when no conditions) or with `$match` stage first when conditions exist.

Condition JSON fragments (built in C#, then injected into `match.scriban` as the `conditions` list):
- market → `"address.market": "Los Angeles"`
- price min → `"price": { "$gte": 150 }` ; price max → `"price": { "$lte": 250 }` (merge into a single `price` object)
- beds/bedrooms min → `"beds": { "$gte": 3 }` (bedrooms use `"bedrooms"`)
- amenities → `"amenities": { "$all": ["Pool"] }`

- [ ] **Step 1: Write the failing tests**

Create `tests/Gateway.Tests/Nlp/ScribanSimpleMqlBuilderTests.cs`:

```csharp
using System.Text.Json;
using Gateway.Nlp.Mql;
using Gateway.Nlp.Slots;

namespace Gateway.Tests.Nlp;

public class ScribanSimpleMqlBuilderTests
{
    private static readonly IMqlBuilder Builder = ScribanSimpleMqlBuilder.FromAssetsDirectory(
        Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Templates"));

    private static JsonElement ParseArray(string json)
    {
        var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        return doc.RootElement.Clone();
    }

    [Fact]
    public void Defaults_only_produces_sort_and_limit()
    {
        var json = Builder.Build(ExtractedSlots.Empty, MqlDefaults.Standard);

        var stages = ParseArray(json);
        Assert.Equal(2, stages.GetArrayLength());
        Assert.Equal("$sort", stages[0].EnumerateObject().First().Name);
        Assert.Equal("$limit", stages[1].EnumerateObject().First().Name);
    }

    [Fact]
    public void Market_slot_produces_match_stage()
    {
        var slots = new ExtractedSlots("Los Angeles", null, null, null, null, new List<string>(), false);

        var json = Builder.Build(slots, MqlDefaults.Standard);

        var stages = ParseArray(json);
        Assert.Equal(3, stages.GetArrayLength());
        var match = stages[0];
        var field = match.GetProperty("$match");
        Assert.Equal("Los Angeles", field.GetProperty("address.market").GetString());
    }

    [Fact]
    public void Price_range_produces_gte_and_lte_merged()
    {
        var slots = new ExtractedSlots(null, null, null, 150m, 250m, new List<string>(), false);

        var json = Builder.Build(slots, MqlDefaults.Standard);

        var match = JsonDocument.Parse(json).RootElement[0].GetProperty("$match");
        var price = match.GetProperty("price");
        Assert.Equal(150, price.GetProperty("$gte").GetInt32());
        Assert.Equal(250, price.GetProperty("$lte").GetInt32());
    }

    [Fact]
    public void Amenities_produce_all_array()
    {
        var slots = new ExtractedSlots(null, null, null, null, null, new List<string> { "Pool", "Kitchen" }, false);

        var json = Builder.Build(slots, MqlDefaults.Standard);

        var match = JsonDocument.Parse(json).RootElement[0].GetProperty("$match");
        var amenities = match.GetProperty("amenities").GetProperty("$all");
        Assert.Equal("Pool", amenities[0].GetString());
        Assert.Equal("Kitchen", amenities[1].GetString());
    }

    [Fact]
    public void Bedrooms_produce_bedrooms_field()
    {
        var slots = new ExtractedSlots(null, 2, null, null, null, new List<string>(), false);

        var json = Builder.Build(slots, MqlDefaults.Standard);

        var match = JsonDocument.Parse(json).RootElement[0].GetProperty("$match");
        Assert.Equal(2, match.GetProperty("bedrooms").GetProperty("$gte").GetInt32());
    }

    [Fact]
    public void Sort_and_limit_reflect_defaults()
    {
        var json = Builder.Build(ExtractedSlots.Empty, MqlDefaults.Standard);

        var stages = ParseArray(json);
        Assert.Equal("review_scores.rating", stages[0].GetProperty("$sort").EnumerateObject().First().Name);
        Assert.Equal(-1, stages[0].GetProperty("$sort").EnumerateObject().First().Value.GetInt32());
        Assert.Equal(10, stages[1].GetProperty("$limit").GetInt32());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ScribanSimpleMqlBuilderTests"`
Expected: FAIL — types not defined.

- [ ] **Step 3: Implement**

Create `src/gateway/Nlp/Mql/IMqlBuilder.cs`:

```csharp
namespace Gateway.Nlp.Mql;

public sealed record MqlDefaults(int Limit, string Sort, string Market)
{
    public static readonly MqlDefaults Standard = new(10, "rating_desc", "All");
}

public interface IMqlBuilder
{
    string Build(ExtractedSlots slots, MqlDefaults defaults);
}
```

Create `src/gateway/Nlp/Mql/ScribanSimpleMqlBuilder.cs`:

```csharp
using System.Text;
using System.Text.Json;
using Gateway.Nlp.Slots;
using Scriban;

namespace Gateway.Nlp.Mql;

public sealed class ScribanSimpleMqlBuilder : IMqlBuilder
{
    private readonly Template _match;
    private readonly Template _sort;
    private readonly Template _limit;

    private ScribanSimpleMqlBuilder(string templatesDirectory)
    {
        _match = Template.Parse(File.ReadAllText(Path.Combine(templatesDirectory, "match.scriban")));
        _sort = Template.Parse(File.ReadAllText(Path.Combine(templatesDirectory, "sort.scriban")));
        _limit = Template.Parse(File.ReadAllText(Path.Combine(templatesDirectory, "limit.scriban")));
    }

    public static ScribanSimpleMqlBuilder FromAssetsDirectory(string templatesDirectory)
    {
        return new ScribanSimpleMqlBuilder(templatesDirectory);
    }

    public string Build(ExtractedSlots slots, MqlDefaults defaults)
    {
        var stages = new List<string>();
        var conditions = BuildConditions(slots);

        if (conditions.Count > 0)
        {
            stages.Add(_match.Render(new { has_conditions = true, conditions }));
        }

        var (sortField, sortDirection) = ResolveSort(defaults.Sort);
        stages.Add(_sort.Render(new { sort_field = sortField, sort_direction = sortDirection }));
        stages.Add(_limit.Render(new { limit = slots.JustRunIt ? defaults.Limit : defaults.Limit }));

        return "[" + string.Join(",", stages) + "]";
    }

    private static List<string> BuildConditions(ExtractedSlots slots)
    {
        var conditions = new List<string>();

        if (slots.Market is not null)
        {
            conditions.Add($"\"address.market\": {JsonSerializer.Serialize(slots.Market)}");
        }

        var priceParts = new List<string>();
        if (slots.MinPrice is decimal min)
        {
            priceParts.Add($"\"$gte\": {JsonSerializer.Serialize(min)}");
        }

        if (slots.MaxPrice is decimal max)
        {
            priceParts.Add($"\"$lte\": {JsonSerializer.Serialize(max)}");
        }

        if (priceParts.Count > 0)
        {
            conditions.Add($"\"price\": {{ {string.Join(", ", priceParts)} }}");
        }

        if (slots.MinBeds is int beds)
        {
            conditions.Add($"\"beds\": {{ \"$gte\": {beds} }}");
        }

        if (slots.MinBedrooms is int bedrooms)
        {
            conditions.Add($"\"bedrooms\": {{ \"$gte\": {bedrooms} }}");
        }

        if (slots.Amenities.Count > 0)
        {
            var all = string.Join(",", slots.Amenities.Select(JsonSerializer.Serialize));
            conditions.Add($"\"amenities\": {{ \"$all\": [{all}] }}");
        }

        return conditions;
    }

    private static (string Field, int Direction) ResolveSort(string sort)
    {
        return sort switch
        {
            "rating_desc" => ("review_scores.rating", -1),
            "price_asc" => ("price", 1),
            "price_desc" => ("price", -1),
            _ => ("review_scores.rating", -1)
        };
    }
}
```

Note: templates use `{{ if !for.last }}, {{ end }}` inside the loop — validate that Scriban renders `for.last` correctly. If the match template loop emits a trailing comma, tighten to the fragment-join approach:

`match.scriban` loop join: replace with pre-joined `conditions_joined` string in code and a simple template:

```text
{{ if has_conditions }}{ "$match": { {{ conditions_joined }} } }{{ end }}
```

and code: `var conditionsJoined = string.Join(", ", conditions);` then `_match.Render(new { has_conditions = conditions.Count > 0, conditions_joined = conditionsJoined })`. Update the render call accordingly if the loop approach misbehaves. The tests assert exact JSON structure, so any rendering bug surfaces immediately.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ScribanSimpleMqlBuilderTests"`
Expected: all 6 PASS.

- [ ] **Step 5: Commit**

```bash
cd /workspace
git add src/gateway/Nlp/Mql tests/Gateway.Tests/Nlp/ScribanSimpleMqlBuilderTests.cs
git commit -m "feat: add Scriban simple MQL builder"
```

---

### Task 8: NLP router

**Files:**
- Create: `src/gateway/Nlp/Router/INlpRouter.cs`
- Create: `src/gateway/Nlp/Router/NlpRouter.cs`
- Create: `src/gateway/Nlp/Router/NlpRouteResult.cs`
- Test: `tests/Gateway.Tests/Nlp/NlpRouterTests.cs`

**Interfaces:**
- Consumes: `ITextEmbedder`, `ISemanticCache`, `SlotExtractor.Extract`, `IntentClassifier.Classify`, `IMqlBuilder.Build`, `MqlDefaults`, `ExtractedSlots`.
- Produces:
  - `Gateway.Nlp.Router.NlpRouteKind` — `enum { CacheHit, SimpleMql, ClarifyRequired, ComplexLlmRequired }`.
  - `Gateway.Nlp.Router.NlpRouteResult` — record `(NlpRouteKind Kind, string? Mql, string? Question, bool SemanticCacheHit, bool SlotExtractionUsed, IntentKind Intent, bool JustRunIt, MqlDefaults ClarificationsApplied, int LlmTokensConsumed)`.
  - `Gateway.Nlp.Router.INlpRouter` — `NlpRouteResult Route(string utterance)`.
  - `Gateway.Nlp.Router.NlpRouter` — ctor `(ITextEmbedder embedder, ISemanticCache cache, IMqlBuilder mqlBuilder)`; pipeline as in the spec.

- [ ] **Step 1: Write the failing tests**

Create `tests/Gateway.Tests/Nlp/NlpRouterTests.cs`:

```csharp
using Gateway.Nlp.Abstractions;
using Gateway.Nlp.Cache;
using Gateway.Nlp.Intent;
using Gateway.Nlp.Mql;
using Gateway.Nlp.Router;

namespace Gateway.Tests.Nlp;

public class NlpRouterTests
{
    private sealed class VectorEmbedder : ITextEmbedder
    {
        public Dictionary<string, float[]> Cache { get; } = new();

        public float[] Embed(string text)
        {
            if (Cache.TryGetValue(text, out var v)) return v;

            var h = text.GetHashCode();
            var vec = new float[8];
            for (var i = 0; i < vec.Length; i++)
            {
                vec[i] = (float)Math.Sin(h * (i + 1) * 0.13);
            }

            var norm = Math.Sqrt(vec.Sum(x => (double)x * x));
            for (var i = 0; i < vec.Length; i++) vec[i] = (float)(vec[i] / norm);

            Cache[text] = vec;
            return vec;
        }
    }

    private readonly VectorEmbedder _embedder = new();
    private readonly NlpRouter _router;
    private readonly IMqlBuilder _builder;
    private readonly ISemanticCache _cache;

    public NlpRouterTests()
    {
        _cache = new SemanticCache();
        _builder = ScribanSimpleMqlBuilder.FromAssetsDirectory(
            Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Templates"));
        _router = new NlpRouter(_embedder, _cache, _builder);
    }

    [Fact]
    public void Acceptance_phrase_returns_simple_mql_with_zero_tokens()
    {
        var result = _router.Route("listings with pools in Los Angeles, just run it");

        Assert.Equal(NlpRouteKind.SimpleMql, result.Kind);
        Assert.False(result.SemanticCacheHit);
        Assert.True(result.SlotExtractionUsed);
        Assert.Equal(IntentKind.Search, result.Intent);
        Assert.Equal(0, result.LlmTokensConsumed);
        Assert.Contains("address.market", result.Mql);
    }

    [Fact]
    public void Repeat_query_hits_semantic_cache()
    {
        _router.Route("listings with pools in Los Angeles, just run it");

        var result = _router.Route("listings with pools in Los Angeles, just run it");

        Assert.Equal(NlpRouteKind.CacheHit, result.Kind);
        Assert.True(result.SemanticCacheHit);
        Assert.Equal(0, result.LlmTokensConsumed);
    }

    [Fact]
    public void Complex_unstructured_query_is_complex()
    {
        var result = _router.Route("coziest neighborhoods near the beach by season");

        Assert.Equal(NlpRouteKind.ComplexLlmRequired, result.Kind);
        Assert.Null(result.Mql);
        Assert.Equal(0, result.LlmTokensConsumed);
    }

    [Fact]
    public void Clarify_intent_returns_single_question()
    {
        var result = _router.Route("what can you do?");

        Assert.Equal(NlpRouteKind.ClarifyRequired, result.Kind);
        Assert.NotNull(result.Question);
        Assert.Null(result.Mql);
    }

    [Fact]
    public void Just_run_it_with_sparse_slots_still_simple()
    {
        var result = _router.Route("listings, just run it");

        Assert.Equal(NlpRouteKind.SimpleMql, result.Kind);
        Assert.True(result.JustRunIt);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~NlpRouterTests"`
Expected: FAIL — types not defined.

- [ ] **Step 3: Implement**

Create `src/gateway/Nlp/Router/NlpRouteResult.cs`:

```csharp
using Gateway.Nlp.Intent;
using Gateway.Nlp.Mql;

namespace Gateway.Nlp.Router;

public enum NlpRouteKind
{
    CacheHit,
    SimpleMql,
    ClarifyRequired,
    ComplexLlmRequired
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
    int LlmTokensConsumed);
```

Create `src/gateway/Nlp/Router/INlpRouter.cs`:

```csharp
namespace Gateway.Nlp.Router;

public interface INlpRouter
{
    NlpRouteResult Route(string utterance);
}
```

Create `src/gateway/Nlp/Router/NlpRouter.cs`:

```csharp
using Gateway.Nlp.Abstractions;
using Gateway.Nlp.Cache;
using Gateway.Nlp.Intent;
using Gateway.Nlp.Mql;
using Gateway.Nlp.Slots;

namespace Gateway.Nlp.Router;

public sealed class NlpRouter : INlpRouter
{
    private readonly ITextEmbedder _embedder;
    private readonly ISemanticCache _cache;
    private readonly IMqlBuilder _mqlBuilder;
    private readonly Gazetteer _gazetteer;
    private readonly IntentClassifier _intentClassifier = new();

    public NlpRouter(ITextEmbedder embedder, ISemanticCache cache, IMqlBuilder mqlBuilder)
        : this(embedder, cache, mqlBuilder, Gazetteer.LoadFromDirectory(
            Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Gazetteers")))
    {
    }

    public NlpRouter(ITextEmbedder embedder, ISemanticCache cache, IMqlBuilder mqlBuilder, Gazetteer gazetteer)
    {
        _embedder = embedder;
        _cache = cache;
        _mqlBuilder = mqlBuilder;
        _gazetteer = gazetteer;
    }

    public NlpRouteResult Route(string utterance)
    {
        var normalized = utterance.Trim();
        var vector = _embedder.Embed(normalized);

        var cached = _cache.TryFind(vector);
        if (cached is not null)
        {
            return new NlpRouteResult(
                NlpRouteKind.CacheHit, cached.Mql, null,
                SemanticCacheHit: true, SlotExtractionUsed: false,
                Intent: IntentKind.Search, JustRunIt: false,
                ClarificationsApplied: MqlDefaults.Standard, LlmTokensConsumed: 0);
        }

        var intent = _intentClassifier.Classify(normalized);
        if (intent.Kind == IntentKind.Clarify)
        {
            return new NlpRouteResult(
                NlpRouteKind.ClarifyRequired, null, intent.ClarificationQuestion,
                SemanticCacheHit: false, SlotExtractionUsed: false,
                Intent: intent.Kind, JustRunIt: false,
                ClarificationsApplied: MqlDefaults.Standard, LlmTokensConsumed: 0);
        }

        var slots = SlotExtractor.Extract(normalized, _gazetteer);
        var defaults = MqlDefaults.Standard;

        if (intent.IsComplex && !slots.HasAnyConstraints)
        {
            return new NlpRouteResult(
                NlpRouteKind.ComplexLlmRequired, null, null,
                SemanticCacheHit: false, SlotExtractionUsed: slots.HasAnyConstraints,
                Intent: intent.Kind, JustRunIt: slots.JustRunIt,
                ClarificationsApplied: defaults, LlmTokensConsumed: 0);
        }

        var mql = _mqlBuilder.Build(slots, defaults);
        _cache.Store(vector, normalized, mql);

        return new NlpRouteResult(
            NlpRouteKind.SimpleMql, mql, null,
            SemanticCacheHit: false, SlotExtractionUsed: slots.HasAnyConstraints,
            Intent: intent.Kind, JustRunIt: slots.JustRunIt,
            ClarificationsApplied: defaults, LlmTokensConsumed: 0);
    }
}
```

Note: `ComplexLlmRequired` is only returned when the query is complex AND has no extractable constraints. If a complex utterance still carries slots (e.g. "average price of 2 bedroom listings in LA"), it has constraints so it falls through to `SimpleMql` this sprint — matching the spec decision table ("no slots + semantic wording"). If the router unit test `Complex_unstructured_query_is_complex` uses a phrase with no slots ("coziest neighborhoods near the beach by season"), it returns `ComplexLlmRequired`. Good.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~NlpRouterTests"`
Expected: all 5 PASS.

- [ ] **Step 5: Commit**

```bash
cd /workspace
git add src/gateway/Nlp/Router tests/Gateway.Tests/Nlp/NlpRouterTests.cs
git commit -m "feat: add hybrid NLP router"
```

---

### Task 9: Wire DI, config binding, and the `/api/nlp/query` endpoint

**Files:**
- Create: `src/gateway/Nlp/NlpOptions.cs`
- Create: `src/gateway/Nlp/NlpServiceCollectionExtensions.cs`
- Create: `src/gateway/Nlp/Http/NlpQueryRequest.cs`
- Create: `src/gateway/Nlp/Http/NlpQueryResponse.cs`
- Modify: `src/gateway/Program.cs`
- Test: `tests/Gateway.Tests/ApiContractTests.cs` (append endpoint tests)

**Interfaces:**
- Consumes: all Tasks 2–8 types.
- Produces:
  - `Gateway.Nlp.NlpOptions` bound from config section `Nlp` (`Embeddings:ModelPath`, `Embeddings:TokenizerPath`, `Cache:Threshold`, `Defaults:*`).
  - `Gateway.Nlp.NlpServiceCollectionExtensions.AddGatewayNlp(this IServiceCollection, IConfiguration)` — registers `ITextEmbedder` (singleton `OnnxBgeSmallEmbedder`, path resolved relative to content root via `IHostEnvironment`), `ISemanticCache`, `IMqlBuilder`, `INlpRouter` (all singleton); path resolution: if configured path is relative, combine with `hostEnvironment.ContentRootPath`.
  - `POST /api/nlp/query` mapped in `Program.cs` inside the authenticated section; accepts `{ utterance }`, returns typed response; validation rejects null/empty/too-long utterances (400).

- [ ] **Step 1: Write the failing endpoint tests (append to existing `ApiContractTests.cs`)**

```csharp
    [Fact]
    public async Task Nlp_query_without_token_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/nlp/query", new { utterance = "listings in Los Angeles" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Nlp_query_simple_utterance_returns_simple_mql_zero_tokens()
    {
        var client = AuthedClient();

        var response = await client.PostAsJsonAsync("/api/nlp/query", new { utterance = "listings with pools in Los Angeles, just run it" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<NlpQueryDto>();
        Assert.NotNull(payload);
        Assert.Equal("SimpleMql", payload!.Kind);
        Assert.Equal(0, payload.LlmTokensConsumed);
        Assert.Contains("address.market", payload.Mql);
    }

    [Fact]
    public async Task Nlp_query_identical_repeat_hits_cache()
    {
        var client = AuthedClient();
        var body = new { utterance = "listings with pools in Los Angeles, just run it" };

        await client.PostAsJsonAsync("/api/nlp/query", body);
        var second = await client.PostAsJsonAsync("/api/nlp/query", body);

        var payload = await second.Content.ReadFromJsonAsync<NlpQueryDto>();
        Assert.Equal("CacheHit", payload!.Kind);
        Assert.True(payload.SemanticCacheHit);
    }
```

Add the DTO record near the other private records at the bottom of `ApiContractTests.cs`:

```csharp
    private sealed record NlpQueryDto(
        string Kind,
        string? Mql,
        string? Question,
        bool SemanticCacheHit,
        bool SlotExtractionUsed,
        string Intent,
        bool JustRunIt,
        int LlmTokensConsumed);
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ApiContractTests"`
Expected: FAIL — endpoint returns 404 (not wired yet).

- [ ] **Step 3: Implement options, DI, DTOs**

Create `src/gateway/Nlp/NlpOptions.cs`:

```csharp
namespace Gateway.Nlp;

public sealed class NlpOptions
{
    public const string SectionName = "Nlp";

    public EmbeddingsOptions Embeddings { get; set; } = new();
    public CacheOptions Cache { get; set; } = new();
    public DefaultsOptions Defaults { get; set; } = new();

    public sealed class EmbeddingsOptions
    {
        public string ModelPath { get; set; } = "Models/bge-small-en-v1.5/model_quantized.onnx";
        public string TokenizerPath { get; set; } = "Models/bge-small-en-v1.5/vocab.txt";
        public int MaxTokens { get; set; } = 512;
    }

    public sealed class CacheOptions
    {
        public double Threshold { get; set; } = 0.95;
    }

    public sealed class DefaultsOptions
    {
        public int Limit { get; set; } = 10;
        public string Sort { get; set; } = "rating_desc";
        public string Market { get; set; } = "All";
    }
}
```

Create `src/gateway/Nlp/NlpServiceCollectionExtensions.cs`:

```csharp
using Gateway.Nlp.Abstractions;
using Gateway.Nlp.Cache;
using Gateway.Nlp.Embeddings;
using Gateway.Nlp.Mql;
using Gateway.Nlp.Router;
using Microsoft.Extensions.Options;

namespace Gateway.Nlp;

public static class NlpServiceCollectionExtensions
{
    public static IServiceCollection AddGatewayNlp(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NlpOptions>(configuration.GetSection(NlpOptions.SectionName));

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
        services.AddSingleton<INlpRouter>(sp => new NlpRouter(
            sp.GetRequiredService<ITextEmbedder>(),
            sp.GetRequiredService<ISemanticCache>(),
            sp.GetRequiredService<IMqlBuilder>()));

        return services;
    }

    private static string Resolve(IHostEnvironment env, string path)
    {
        return Path.IsPathRooted(path) ? path : Path.Combine(env.ContentRootPath, path);
    }
}
```

Create `src/gateway/Nlp/Http/NlpQueryRequest.cs`:

```csharp
namespace Gateway.Nlp.Http;

public sealed record NlpQueryRequest(string? Utterance);
```

Create `src/gateway/Nlp/Http/NlpQueryResponse.cs`:

```csharp
using Gateway.Nlp.Intent;
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
    int LlmTokensConsumed)
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
            result.LlmTokensConsumed);
    }
}
```

- [ ] **Step 4: Wire the endpoint in `Program.cs`**

After `app.UseAuthorization();` add the service registration before `app.Build()` is NOT possible (services must be added before Build) — so add `builder.Services.AddGatewayNlp(builder.Configuration);` alongside the existing registrations (after `builder.Services.AddAuthorization();`):

```csharp
builder.Services.AddGatewayNlp(builder.Configuration);
```

Add the endpoint after the greeting endpoint (still inside the file, before the SSE stream or after — order irrelevant in Minimal APIs), using a helper that maps the route:

```csharp
app.MapPost("/api/nlp/query", (NlpQueryRequest request, INlpRouter router) =>
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

    return Results.Ok(NlpQueryResponse.From(router.Route(utterance)));
}).RequireAuthorization();
```

Also add the using directives at the top of `Program.cs`:

```csharp
using Gateway.Nlp;
using Gateway.Nlp.Http;
using Gateway.Nlp.Router;
```

Because `builder.Services` must be registered before `builder.Build()`, place `builder.Services.AddGatewayNlp(builder.Configuration);` right after the existing `builder.Services.AddAuthorization();` line.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj`
Expected: full suite green — Sprint 1 API tests plus new NLP endpoint tests (401, SimpleMql zero tokens, cache-hit repeat) all PASS. (Requires the model present; `TestPaths` guard is only in embedder tests, and the API factory resolves the embedder lazily, so the singleton is created on first `/api/nlp/query` call.)

- [ ] **Step 6: Commit**

```bash
cd /workspace
git add src/gateway/Nlp/NlpOptions.cs src/gateway/Nlp/NlpServiceCollectionExtensions.cs src/gateway/Nlp/Http src/gateway/Program.cs tests/Gateway.Tests/ApiContractTests.cs
git commit -m "feat: expose POST /api/nlp/query behind JWT"
```

---

### Task 10: Bench numbers for BRD-NFR-01 and acceptance check

**Files:**
- Create: `tests/Gateway.Tests/Nlp/NfrBenchTests.cs`
- Create: `docs/benchmarks/sprint-2-nfr01.md`
- Modify: `sprints/sprint-2.md` (tick acceptance criteria)
- Test: run full suite

**Interfaces:**
- Consumes: all components.
- Produces: measured bench numbers recorded in `docs/benchmarks/sprint-2-nfr01.md`; sprint acceptance checkboxes completed.

- [ ] **Step 1: Write the bench test (reporting + loose budget asserts)**

Create `tests/Gateway.Tests/Nlp/NfrBenchTests.cs`:

```csharp
using System.Diagnostics;
using Gateway.Nlp.Cache;
using Gateway.Nlp.Embeddings;
using Gateway.Nlp.Mql;
using Gateway.Nlp.Slots;
using Gateway.Nlp.Router;

namespace Gateway.Tests.Nlp;

public class NfrBenchTests
{
    private const int Iterations = 200;

    [Fact]
    public void Records_cache_lookup_slot_and_mql_benchmarks()
    {
        var gazetteer = Gazetteer.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Gazetteers"));
        var builder = ScribanSimpleMqlBuilder.FromAssetsDirectory(
            Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Templates"));
        var embedder = new OnnxBgeSmallEmbedder(TestPaths.ModelPath(), TestPaths.VocabPath());
        var cache = new SemanticCache();
        var router = new NlpRouter(embedder, cache, builder);

        // warm model + router
        router.Route("listings with pools in Los Angeles, just run it");
        router.Route("listings with pools in Los Angeles, just run it");

        var utterance = "listings with pools in Los Angeles, just run it";

        var cacheHitMs = Measure(() => router.Route(utterance));
        var embedMs = Measure(() => embedder.Embed(utterance));
        var slotMs = Measure(() => SlotExtractor.Extract(utterance, gazetteer));
        var mqlMs = Measure(() => builder.Build(SlotExtractor.Extract(utterance, gazetteer), MqlDefaults.Standard));

        var row = $"| cache hit (full route incl. embed) | {cacheHitMs:F2} ms | < 10 ms (lookup) / embed measured |\n" +
                  $"| real ONNX embed | {embedMs:F2} ms | measured ≈ 25 ms |\n" +
                  $"| slot extraction | {slotMs:F3} ms | < 5 ms |\n" +
                  $"| simple MQL render | {mqlMs:F3} ms | < 2 ms |\n";
        File.AppendAllText(Path.Combine(TestPaths.RepoRoot(), "docs", "benchmarks", "sprint-2-nfr01.md"), row);

        // Budget asserts on in-memory steps (embed excluded from cache budget assert)
        Assert.True(slotMs < 5, $"slot extraction {slotMs:F3} ms exceeded 5 ms");
        Assert.True(mqlMs < 2, $"simple MQL render {mqlMs:F3} ms exceeded 2 ms");
    }

    private static double Measure(Action action)
    {
        for (var i = 0; i < 10; i++) action(); // warm
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < Iterations; i++) action();
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds / Iterations;
    }
}
```

- [ ] **Step 2: Create benchmarks directory + doc**

Create `docs/benchmarks/sprint-2-nfr01.md`:

```markdown
# Sprint 2 — BRD-NFR-01 Benchmarks

Measured in this environment (linux-x64 container, Release build). Budget per BRD-NFR-01
for in-memory steps; real ONNX embed recorded separately (split-budget decision).

| Step | Measured | Budget |
| --- | --- | --- |
<!-- NfrBenchTests appends rows here on each run -->
```

- [ ] **Step 3: Run and record numbers**

Run: `cd /workspace && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~NfrBenchTests" -c Release`
Expected: PASS; `docs/benchmarks/sprint-2-nfr01.md` now contains a data row. Verify numbers and that cache-lookup/slot/MQL budgets hold.

Note: this test writes a markdown row on every run — delete stale duplicate rows if reruns append. To keep deterministic, in the doc generation step remove existing table rows (lines beginning with `| ` other than the header) before appending. Simplest for now: run once and commit; if rerun appends duplicates, manually dedupe in the same commit.

- [ ] **Step 4: Tick acceptance criteria in `sprints/sprint-2.md`**

Change the four `- [ ]` acceptance lines to `- [x]`, and (optionally) append a "Status" note line under Exit artifacts listing the committed artifacts (files) and bench doc path.

- [ ] **Step 5: Full suite + commit**

Run: `cd /workspace && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj -c Release`
Expected: entire suite green (Sprint 1 + Sprint 2).

```bash
cd /workspace
git add tests/Gateway.Tests/Nlp/NfrBenchTests.cs docs/benchmarks sprints/sprint-2.md
git commit -m "docs: record Sprint 2 NFR-01 benches and tick acceptance criteria"
git push
```

---

## Notes for the executor

- Each task is independently committable and testable; run `dotnet test` scoped to the filter shown in each task before committing.
- The model + vocab must be present before Tasks 3/9/10 run: `./scripts/download-nlp-assets.sh`.
- If `dotnet` is not on PATH, prefix with `DOTNET_ROOT=/root/.dotnet` and add `/root/.dotnet` to PATH (as done in Sprint 1).
- `docs/BRD.md`, `sprints/sprint-0.md`, and the SPA are intentionally not modified by this sprint (except the sprint-2.md acceptance ticks). Update `sprints/sprint-0.md` only if the sprint index format requires it (check Sprint 1 precedent — it did not list sprint files).
- If Scriban `for.last` rendering is problematic in the match template, fall back to the `conditions_joined` variant noted in Task 7 — tests pin the exact JSON shape either way.
- WebApplicationFactory resolves the embedder lazily; endpoints not touching NLP (health/greeting/SSE) work even before model download, keeping Sprint 1 tests independent.
