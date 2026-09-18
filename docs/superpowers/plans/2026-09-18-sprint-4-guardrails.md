# Sprint 4 Implementation Plan — Guardrails: MQL AST and Field-Flag Governance

**Spec:** `sprints/sprint-4.md`
**Branch:** `260918-feat-sprint-4-guardrails`
**Goal:** Reject unsafe MQL and pause any query that targets sensitive registry fields.

**Approach:** Add a `Gateway.Nlp.Guardrails` module that (1) parses a generated pipeline into field paths and operators, (2) rejects write/admin operators and unparseable input, (3) enforces a schema whitelist, and (4) pauses when a referenced field is sensitive and requires approval. The orchestrator runs the guardrail after query generation and before returning/executing, publishing a `governance.paused` event and creating a `PENDING_LEAD` access request.

Each task below is one commit.

## Global constraints

- No new NuGet packages; use `System.Text.Json`.
- Fail closed: anything unparseable is rejected.
- Keep the guardrail step under 1 ms for typical pipelines.
- New assets live under `src/gateway/Nlp/Assets/Schema/` and are copied to output.
- Existing positional records may only gain trailing optional parameters with defaults.

---

### Task 1: Plan document

**Files:** Create `docs/superpowers/plans/2026-09-18-sprint-4-guardrails.md`

Commit: `docs: add sprint 4 guardrails implementation plan`

---

### Task 2: MQL analysis and field-path extraction (S4-01)

**Files:**
- Create `src/gateway/Nlp/Guardrails/MqlAnalysis.cs`
- Create `src/gateway/Nlp/Guardrails/MqlAnalyzer.cs`
- Test `tests/Gateway.Tests/Nlp/MqlAnalyzerTests.cs`

**Interfaces:**
```csharp
public sealed record MqlAnalysis(
    bool IsParsable,
    string? ParseError,
    IReadOnlyList<string> FieldPaths,
    IReadOnlyList<string> Operators);

public static class MqlAnalyzer
{
    public static MqlAnalysis Analyze(string? pipelineJson);
}
```
- Field paths come from `$match` filter keys, `$sort` keys, `$project`/`$addFields` keys, `$group` `_id` and accumulator inputs, `$unwind` path.
- Operators are every `$`-prefixed stage key plus every `$`-prefixed operator key inside expressions.
- Unparseable JSON or a non-array root returns `IsParsable = false` with a reason.

Commit: `feat(guardrails): add mql analyzer with field-path extraction`

---

### Task 3: Read-only enforcement (S4-02, BRD-FR-04, NFR-08)

**Files:**
- Create `src/gateway/Nlp/Guardrails/ReadOnlyRule.cs`
- Modify `src/gateway/Nlp/Guardrails/MqlAnalyzer.cs` (surface blocked operators)
- Test `tests/Gateway.Tests/Nlp/ReadOnlyRuleTests.cs`

**Interfaces:**
```csharp
public static class ReadOnlyRule
{
    public static readonly IReadOnlySet<string> BlockedOperators; // $out, $merge, drop, deleteMany, ...
    public static IReadOnlyList<string> FindViolations(MqlAnalysis analysis);
}
```
- Blocks write/admin stages and commands: `$out`, `$merge`, `drop`, `deleteMany`, `deleteOne`, `updateMany`, `updateOne`, `insertMany`, `insertOne`, `replaceOne`, `findAndModify`, `renameCollection`, `dropDatabase`, `create`, `createIndex`.
- Unparseable analysis is a violation.

Commit: `feat(guardrails): enforce read-only mql and block write operators`

---

### Task 4: Schema whitelist (S4-03)

**Files:**
- Create `src/gateway/Nlp/Guardrails/SchemaWhitelist.cs`
- Test `tests/Gateway.Tests/Nlp/SchemaWhitelistTests.cs`

**Interfaces:**
```csharp
public sealed class SchemaWhitelist
{
    public static SchemaWhitelist FromFields(IEnumerable<string> fields);
    public static SchemaWhitelist LoadFromSchemaFile(string path);
    public bool IsKnown(string fieldPath);
    public IReadOnlyList<string> FindUnknown(IEnumerable<string> fieldPaths);
}
```
- Parses the `- field: type` lines from `Nlp/Assets/Prompts/schema.txt`.
- Prefix matching: `address.location.coordinates` is known if `address` is a declared prefix; exact match otherwise.
- Unknown paths are rejected unless known.

Commit: `feat(guardrails): add schema field whitelist`

---

### Task 5: Sensitivity registry and seed data (S4-04 part 1)

**Files:**
- Create `src/gateway/Nlp/Guardrails/SensitiveFieldFlag.cs`
- Create `src/gateway/Nlp/Guardrails/ISensitiveFieldRegistry.cs`
- Create `src/gateway/Nlp/Guardrails/InMemorySensitiveFieldRegistry.cs`
- Create `src/gateway/Nlp/Assets/Schema/field_registry.json`
- Modify `src/gateway/Gateway.csproj` (copy `Nlp/Assets/Schema/*.json`)
- Test `tests/Gateway.Tests/Nlp/SensitiveFieldRegistryTests.cs`
- Modify `tests/Gateway.Tests/Nlp/GatewayCsprojTests.cs`

**Interfaces:**
```csharp
public sealed record SensitiveFieldFlag(
    string Path,
    bool IsSensitive,
    bool RequiresApproval,
    IReadOnlyList<string> DataOwnerRoles);

public interface ISensitiveFieldRegistry
{
    IReadOnlyList<SensitiveFieldFlag> All { get; }
    bool TryGet(string fieldPath, out SensitiveFieldFlag flag);
    IReadOnlyList<SensitiveFieldFlag> Match(IEnumerable<string> fieldPaths);
}
```
- Seed: `address.location.coordinates` and a host verification field, both sensitive + approval-required.
- `Match` uses prefix semantics so `address.location.coordinates.lat` matches the flagged base path.

Commit: `feat(guardrails): add sensitive field registry with seed data`

---

### Task 6: Guardrail evaluation and access request stub (S4-04 part 2)

**Files:**
- Create `src/gateway/Nlp/Guardrails/GuardrailOutcome.cs`
- Create `src/gateway/Nlp/Guardrails/GuardrailResult.cs`
- Create `src/gateway/Nlp/Guardrails/GuardrailEvaluator.cs`
- Create `src/gateway/Nlp/Guardrails/AccessRequest.cs`
- Create `src/gateway/Nlp/Guardrails/IAccessRequestStore.cs`
- Create `src/gateway/Nlp/Guardrails/InMemoryAccessRequestStore.cs`
- Test `tests/Gateway.Tests/Nlp/GuardrailEvaluatorTests.cs`
- Test `tests/Gateway.Tests/Nlp/AccessRequestStoreTests.cs`

**Interfaces:**
```csharp
public enum GuardrailOutcome { Allowed, Rejected, PausedForApproval }

public sealed record GuardrailResult(
    GuardrailOutcome Outcome,
    string? Reason,
    IReadOnlyList<string> SensitiveFields,
    IReadOnlyList<string> UnknownFields,
    IReadOnlyList<string> BlockedOperators);

public sealed class GuardrailEvaluator
{
    public GuardrailResult Evaluate(string? pipelineJson);
}
```
- Order: unparseable -> rejected; blocked operators -> rejected; unknown fields -> rejected; sensitive + requires approval -> paused; else allowed.
- `AccessRequest` shape: `Id`, `SessionId`, `Mql`, `SensitiveFields`, `Status` (`PENDING_LEAD`), `Justification`, `CreatedAt`.

Commit: `feat(guardrails): evaluate pipelines and create pending access requests`

---

### Task 7: Orchestrator integration, pause event, and API surface (S4-05)

**Files:**
- Modify `src/gateway/Nlp/Router/NlpRouteResult.cs` (add `GovernancePaused`, `Rejected`; trailing optional fields)
- Modify `src/gateway/Nlp/Orchestrator/NlpOrchestrator.cs`
- Modify `src/gateway/Nlp/Http/NlpQueryResponse.cs`
- Modify `src/gateway/Nlp/NlpServiceCollectionExtensions.cs`
- Test `tests/Gateway.Tests/Nlp/NlpOrchestratorGuardrailTests.cs`

**Behavior:**
- After any route produces MQL, run the guardrail.
- Allowed -> existing behavior.
- Paused -> `NlpRouteKind.GovernancePaused`, create `PENDING_LEAD` request, publish `governance.paused`, expose `AccessRequestId`, `SensitiveFields`, `GuardrailReason`.
- Rejected -> `NlpRouteKind.Rejected` with `GuardrailReason`.
- New response fields are optional and serialized.

Commit: `feat(guardrails): wire guardrails into the orchestrator and api`

---

### Task 8: Bench, acceptance criteria, and status

**Files:**
- Create `tests/Gateway.Tests/Nlp/Sprint4BenchTests.cs` (guardrail step < 1 ms)
- Modify `sprints/sprint-4.md` (tick acceptance criteria)
- Modify `README.md` (mention guardrails status, if useful)

Commit: `test(guardrails): add sprint 4 bench and tick acceptance criteria`

---

## Self-review

- **Spec coverage:** S4-01 Task 2, S4-02 Task 3, S4-03 Task 4, S4-04 Tasks 5-6, S4-05 Task 7, bench and criteria Task 8. Data Owner auto-approve and execution remain out of scope.
- **Placeholder scan:** no `TODO`/`TBD`; all seed values are concrete.
- **Type consistency:** `GuardrailResult`, `GuardrailOutcome`, and `MqlAnalysis` names are used consistently across tasks.
