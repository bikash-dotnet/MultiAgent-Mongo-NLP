# Sprint 2 — Hybrid NLP: Cache, Slots, and Simple MQL (Design)

**Date:** 2026-09-08
**Branch:** `260908-feat-sprint-2-hybrid-nlp`
**BRD version:** 4.2
**Depends on:** Sprint 1 (delivered)
**Status:** Approved

---

## 1. Goal

Route routine English queries through a local deterministic NLP pipeline so that
simple searches never invoke the NVIDIA LLM. This implements BRD-FR-02 with zero
token cost (BRD-NFR-04) while holding the latency budgets in BRD-NFR-01 for the
local steps and the cosine > 0.95 semantic cache threshold of BRD-NFR-05.

The semantic cache uses a **real** local `bge-small-en-v1.5` ONNX embedder
(`Microsoft.ML.OnnxRuntime`) from this sprint. No embedding is stubbed or deferred.

---

## 2. Scope (stories covered)

| Story | Deliverable |
| --- | --- |
| S2-01 | In-memory semantic cache over real embeddings; cosine > 0.95; returns cached MQL + `semantic_cache_hit=true`; in-memory lookup < 10 ms (real embed measured separately ≈ 25 ms) |
| S2-02 | Slot extraction: market, rooms/beds, amenities, price bounds via regex + gazetteers; < 5 ms; records `slot_extraction_used` |
| S2-03 | Rule-based intent filter: Search / Export / Clarify; Max-1 Clarification Rule at the boundary; silent defaults (`limit(10)`, `sort: rating_desc`, `market: All`) |
| S2-04 | Simple MQL builders via Scriban templates (`$match` / `$limit` / `$sort`); no NVIDIA call; < 2 ms |
| S2-05 | NLP routing service `INlpRouter`: cache → slots → intent → simple MQL **or** `COMPLEX_LLM_REQUIRED`; token counter stays 0 |

### Exit artifacts

- `INlpRouter` with cache / slot / template paths
- Gazetteer + Scriban template assets
- Bench numbers for BRD-NFR-01 local steps (cache, slots, simple MQL)

### Acceptance criteria (from `sprints/sprint-2.md`)

- [ ] Known/simple queries resolve via cache or slot/template path with zero LLM tokens (BRD-FR-02)
- [ ] Cache hit only when cosine > 0.95 (BRD-NFR-05)
- [ ] `"listings with pools in Los Angeles, just run it"` produces MQL without NVIDIA
- [ ] Latency budgets in BRD-NFR-01 hold under unit/bench tests for cache, slots, simple MQL

---

## 3. Out of scope (deferred to later sprints)

- NVIDIA few-shot MQL generation (Sprint 3)
- AST guardrails (Sprint 4)
- Governance / schema-field registry / audit writes (Sprint 5)
- Execution against MongoDB and exports (Sprint 6)
- Orchestrator multi-turn clarification loop (Sprint 3; Sprint 2 only classifies `Clarify` intent)

---

## 4. Architecture

```
POST /api/nlp/query { utterance }        (JWT-protected)
        │
NlpRouter.Route(utterance)
  1. Embed(utterance) ───────────────► SemanticCache   cosine > 0.95
        HIT  → CacheHit (returns cached MQL + metadata)
        MISS → continue
  2. SlotExtractor (regex + gazetteers) → market, beds, price bounds, amenities
  3. IntentClassifier → { Search | Export | Clarify }
  4. Routing decision:
       Clarify intent                     → ClarifyRequired (single question, Max-1)
       Search / Export + slots resolvable → ScribanSimpleMqlBuilder → SimpleMql
       Search / Export + no slots,        → ComplexLlmRequired  (no LLM call in Sprint 2)
         unstructured query
  5. Successful SimpleMql results are written back into the cache as the
     "previously approved" MQL artifact (governance arrives in Sprint 5).
```

Every stage is in-memory and deterministic apart from the local ONNX embed;
there are **no** network calls anywhere in the Sprint 2 path. `llmTokensConsumed`
is always `0` for `CacheHit`, `SimpleMql`, and `ClarifyRequired`.

---

## 5. Components

All new code lives in the existing gateway project under `src/gateway/Nlp/**`
(mirroring the folder-per-feature pattern of `Auth/`, `Greeting/`,
`Observability/`). The SPA is untouched this sprint.

| Component | Responsibility |
| --- | --- |
| `Abstractions/ITextEmbedder` | `float[] Embed(string text)`; single embedding contract used by cache and router |
| `Embeddings/OnnxBgeSmallEmbedder` | Loads `bge-small-en-v1.5` int8 ONNX via `InferenceSession`; CLS pooling + L2 normalize |
| `Embeddings/TextTokenizer` | WordPiece encoding (`input_ids`, `attention_mask`) from `vocab.txt` via `Microsoft.ML.Tokenizers` |
| `Cache/SemanticCache` | Thread-safe in-memory store of query → MQL; cosine > 0.95 ⇒ hit; returns stored MQL + metadata |
| `Cache/CosineSimilarity` | Vector cosine similarity with explicit > 0.95 boundary semantics (0.95 exactly = miss) |
| `Cache/CacheEntry` | Stored embedding + generated MQL + metadata for a previously approved query |
| `Slots/SlotExtractor` | Regex + gazetteer matching over normalized utterance |
| `Slots/Gazetteer` | Loads market + amenity dictionaries from JSON assets |
| `Slots/ExtractedSlots` | Typed slot record: market, bedrooms/beds, price min/max, amenities, `justRunIt` flag |
| `Intent/IntentClassifier` | Keyword-rule classification into Search / Export / Clarify |
| `Mql/ScribanSimpleMqlBuilder` | Renders aggregation stages from Scriban templates given extracted slots |
| `Router/NlpRouter` | Orchestrates cache → slots → intent → MQL; injects silent defaults; keeps token counter at 0 |
| `Router/NlpRouteResult` | Typed result (kind, mql, question, metadata flags) |
| `Assets/Gazetteers/markets.json`, `amenities.json` | Gazetteer dictionaries (content-copied to output) |
| `Assets/Templates/*.scriban` | Scriban templates for `$match`, `$sort`, `$limit` stage rendering |
| `scripts/download-nlp-assets.sh` | One-time download of ONNX model + tokenizer into git-ignored `Models/` |

### Example output

For `"listings with pools in Los Angeles, just run it"`:

```json
[
  { "$match": { "address.market": "Los Angeles", "amenities": { "$all": ["Pool"] } } },
  { "$sort": { "review_scores.rating": -1 } },
  { "$limit": 10 }
]
```

with metadata `{ semantic_cache_hit: false, slot_extraction_used: true,
intent: Search, clarifications_applied: { limit: 10, sort: rating_desc } }`.

---

## 6. ONNX embedding layer (real, this sprint)

- NuGet packages added to `src/gateway/Gateway.csproj`:
  - `Microsoft.ML.OnnxRuntime` (native inference runtime, includes linux-x64 native libs)
  - `Microsoft.ML.Tokenizers` (BERT WordPiece tokenizer)
  - `Scriban` (template rendering)
- Model source: `Xenova/bge-small-en-v1.5` on Hugging Face:
  - `onnx/model_quantized.onnx` (~34 MB, int8)
  - `vocab.txt`, `tokenizer.json`, `tokenizer_config.json`
- Provisioning:
  - `scripts/download-nlp-assets.sh` downloads the files into `src/gateway/Models/`
  - `src/gateway/Models/` is git-ignored (repo stays light)
  - Model path is configurable via `Nlp:Embeddings:ModelPath` in `appsettings.json`
    (default resolves to `Models/bge-small-en-v1.5/model_quantized.onnx` next to content root)
- Tokenizer: WordPiece from `vocab.txt` via `Microsoft.ML.Tokenizers`
  (`BertTokenizer.Create(vocabFilePath)`, lowercase + basic tokenization defaults
  matching bge's bert-base-uncased tokenizer); max sequence length 512;
  `EncodeToIds` returns `[CLS]...`-prefixed ids; attention mask is all-ones and
  token-type ids all-zeros.
- Embedding: BGE query-instruction prefix
  `"Represent this sentence for searching relevant passages: "` is prepended to
  every utterance (both cache-write and cache-read, symmetric), then run inference;
  take the last hidden state of the `[CLS]` token (position 0); L2-normalize.
- **Missing-model behavior:** the embedder constructor validates that model + vocab
  files exist and throws an actionable error naming the missing file and the
  download script. It is registered lazily so greeting/health endpoints and
  non-embedding unit tests work without the model; the NLP router/embedding tests
  require `scripts/download-nlp-assets.sh` to have been run first (no silent stub).

### Measured behavior (this environment, Release, int8 model)

- Embedding latency ≈ **25 ms/query** (steady-state, single thread; this is a
  documented measured number — see bench section).
- Identical text → cosine 1.0 (always a cache hit).
- With the instruction prefix, near-paraphrases clear 0.95 with margin:
  `"listings with pools in Los Angeles"` vs `"listings that have pools in
  Los Angeles"` ≈ **0.986**; vs `"find listings that have a pool in Los Angeles"`
  ≈ 0.978; distinct queries (e.g. "luxury condos in New York under 500") ≈
  0.56–0.59 (correct miss).
- Some looser rewordings land 0.88–0.95 (correct miss under the strict `>` 0.95
  gate); those queries fall through to the slot/template path — this is intended
  BRD-NFR-05 behavior, not a defect.

### Cache-hit semantics (empirical)

Cache hit **iff** cosine > 0.95, strictly per BRD-NFR-05. Tests assert:
(a) identical text hits (cosine 1.0); (b) a proven-above-threshold rephrase hits;
(c) a below-threshold rephrase and a distinct query miss and continue to the
slot path. Semantic recall is intentionally conservative — exact and near-exact
repeats hit; loose rephrasings route through deterministic slots.

---

## 7. Slot model and gazetteers

`ExtractedSlots`:

| Field | Type | Source example |
| --- | --- | --- |
| `Market` | `string?` | "in Los Angeles" → `Los Angeles` |
| `MinBedrooms` / `MinBeds` | `int?` | "2 bedrooms" / "3 beds" |
| `MinPrice` / `MaxPrice` | `decimal?` | "under $200" → max 200; "$150-$250" → min/max |
| `Amenities` | `IReadOnlyList<string>` | "with pools" → `["Pool"]` |
| `JustRunIt` | `bool` | utterance contains "just run it" / "go ahead" |

Gazetteer assets:
- `markets.json`: curated market/city names and aliases (e.g. "LA" → "Los Angeles",
  "NYC" → "New York", "SF" → "San Francisco").
- `amenities.json`: amenity phrases mapped to the canonical string used in
  `listingsAndReviews.amenities` (e.g. "pool", "swimming pool" → `Pool`).

Gazetteers are loaded once at startup and injected into `SlotExtractor`; they are
plain JSON content assets copied to the build output.

---

## 8. Intent classification

- **Search**: listings/find/show/which … have/with … (`limit`-able retrieval).
- **Export**: contains export / download / csv / xlsx / send … file signals.
- **Clarify**: utterance is itself a question or greeting that cannot map to a
  listing retrieval or export (e.g. "what can you do?", "hi").
- Silent-default handling: when the user says `"just run it"` or skips non-critical
  slots, defaults are applied and recorded in `clarifications_applied`:
  - `limit: 10`
  - `sort: rating_desc` → `$sort: { review_scores.rating: -1 }`
  - `market: All` → no `$match` on market is emitted.
- Export intent that also needs a search still produces a `SimpleMql` result this
  sprint; the file/export delivery machinery is a later sprint.

---

## 9. API contract

`POST /api/nlp/query` (JWT-protected, same auth as Sprint 1 endpoints)

Request:

```json
{ "utterance": "listings with pools in Los Angeles, just run it" }
```

Response (`200`):

```json
{
  "kind": "SimpleMql",
  "mql": "[{\"$match\":...},{\"$sort\":...},{\"$limit\":10}]",
  "question": null,
  "semanticCacheHit": false,
  "slotExtractionUsed": true,
  "intent": "Search",
  "clarificationsApplied": { "limit": 10, "sort": "rating_desc" },
  "llmTokensConsumed": 0
}
```

- `kind` ∈ `CacheHit | SimpleMql | ClarifyRequired | ComplexLlmRequired`.
- `question` non-null only for `ClarifyRequired` (exactly one question, Max-1 rule).
- No JWT → `401` (verified by tests).
- Request/response DTOs validated with the existing FluentValidation setup if a
  validation convention exists; otherwise minimal DataAnnotations.

---

## 10. Routing decision details

| Condition | Result kind | MQL |
| --- | --- | --- |
| Cache hit (cosine > 0.95) | `CacheHit` | cached MQL |
| Clarify intent (question/greeting, no actionable slots) | `ClarifyRequired` | none |
| Search/Export with ≥1 extracted slot OR just-run-it | `SimpleMql` | Scriban pipeline |
| Search/Export, **browse** wording, no slots (e.g. "show me top listings") | `SimpleMql` | defaults only (`$sort` rating_desc + `$limit` 10, no `$match`) |
| Search/Export, no slots + semantic/comparative/aggregate wording (e.g. "coziest neighborhoods near the beach by season") | `ComplexLlmRequired` | none (no LLM call this sprint) |

The `just run it` phrase never blocks: it forces default injection and produces a
`SimpleMql` even with sparse slots.

The browse-vs-complex boundary is decided by the intent classifier: plain
retrieval verbs without any slot plus generic object words ("listings",
"places", "homes", "show", "browse", "top N/best N listings" where N is a bare
count) route to `SimpleMql` with defaults; grouped/extreme comparisons
("most expensive *by market*", "coziest neighborhood ranking"),
geo/proximity ("near X", "by season"), or aggregation signals route to
`ComplexLlmRequired`. The full boundary definition lives in the intent rule
tables in the implementation plan.

---

## 11. Testing strategy

> Note: the ONNX embedder tests and router/api tests that depend on real
> embeddings require the model + tokenizer assets downloaded via
> `scripts/download-nlp-assets.sh` before `dotnet test`.

Tests live in `tests/Gateway.Tests/` (same project/pattern as Sprint 1) and are
grouped by feature:

- `EmbeddingTests` — tokenizer round-trip; embedder returns fixed-size (384)
  normalized vector; identical text → cosine 1.0; proven-above-threshold rephrase
  → cosine > 0.95; distinct query → well below 0.95 (requires model assets
  present; script run before test).
- `CosineSimilarityTests` — orthogonal vectors ≈ 0; identical = 1.0; exactly 0.95
  is **miss** (boundary), just above is hit.
- `SemanticCacheTests` — add/retrieve; hit returns stored MQL + metadata;
  miss on below-threshold query; concurrency smoke.
- `SlotExtractorTests` — market aliases, "2 bedrooms"/"3 beds", "$150-$250",
  "under $200", "with pools", none-of, case-insensitivity.
- `IntentClassifierTests` — Search / Export / Clarify table cases.
- `ScribanMqlBuilderTests` — JSON parses as array; correct stages per slot combo;
  market All emits no `$match` on market; defaults injected.
- `NlpRouterTests` — integration through the real embedder: known simple query →
  `SimpleMql`, tokens 0; repeat identical query → `CacheHit` with
  `semanticCacheHit=true`; complex unstructured query → `ComplexLlmRequired`;
  "just run it" never escalates to LLM path.
- `ApiContractTests` (additions) — 401 without token; end-to-end POST via
  `WebApplicationFactory` for the acceptance phrase.
- `NfrBenchTests` — Stopwatch benches asserting the in-memory budgets with
  headroom and printing measured numbers for the exit-artifact bench:
  - cache **lookup** (embedding excluded — vector precomputed) < 10 ms
  - slot extraction < 5 ms
  - simple MQL render < 2 ms
  - real ONNX embed recorded separately as a measured number (≈ 25 ms in this
    environment; not asserted against the 10 ms budget per the split-budget
    decision).

The embedder is `IDisposable`-safe in the DI container (disposes the
`InferenceSession`); tests use the shared `WebApplicationFactory` lifetime.

---

## 12. Config additions

`appsettings.json`:

```json
"Nlp": {
  "Embeddings": {
    "ModelPath": "Models/bge-small-en-v1.5/model_quantized.onnx",
    "TokenizerPath": "Models/bge-small-en-v1.5/vocab.txt",
    "MaxTokens": 512
  },
  "Cache": { "Threshold": 0.95 }
}
```

Values overridable by env/config in tests via the existing `WebApplicationFactory`
`UseSetting` pattern used by Sprint 1.

---

## 13. Risk notes

- ONNX native lib availability on linux-x64 is provided by the NuGet runtime
  package; verified in CI via `dotnet test`.
- First-run latency includes model load (one-time); bench tests warm the session
  before measuring steady-state lookup latency.
- The ">0.95 for paraphrases" property is empirical (see Measured behavior);
  the cache threshold remains a strict `>` per the BRD. Loose rewordings that
  score 0.88–0.95 correctly miss the cache and route through deterministic slots.

---

## 14. Out-of-scope reminders (boundary guard)

- No MongoDB execution, no NVIDIA calls, no governance/audit writes, no AST
  guardrail, no export file generation, no orchestrator multi-turn loop.
