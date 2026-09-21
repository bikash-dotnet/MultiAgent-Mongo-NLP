# Report Intake Chat Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the single-shot query panel with a server-driven chat assistant that collects report delivery email, purpose, manager email, columns, and delivery choice, then starts flag-gated approval for sensitive queries.

**Architecture:** The gateway owns a conversation state machine (`Gateway.Conversations`) that wraps the existing `INlpOrchestrator`. A runtime-mutable approval flag (`Gateway.Governance`) decides whether the guardrail pauses sensitive queries. In-memory reports and notifications live in `Gateway.Reports`. The Angular SPA renders the conversation as a chat thread with inline step controls and a role-restricted governance toggle.

**Tech Stack:** ASP.NET Core 10 minimal APIs, System.Text.Json, xUnit, Angular 21 (standalone components, vitest, jsdom).

## Global Constraints

- Target `net10.0`; do not add any new NuGet package.
- Use `System.Text.Json` for serialization; do not add converters for enums on the wire (send step and control as strings).
- Positional records only gain trailing optional parameters with defaults.
- Guardrail logic must fail closed and stay under 1 ms.
- All new persistence is in-memory; no MongoDB access in this slice.
- No new comments in code; match the existing comment-free style.
- Commit messages: `feat(scope): ...` and every commit ends with the trailer `Co-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>`.
- Run gateway tests with `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~<filter>"` after `export PATH="$PATH:/root/.dotnet"`.
- Running the full gateway suite rewrites `docs/benchmarks/*.md`; always `git restore docs/benchmarks/` afterwards before committing.
- Angular tests run with `npm test` in `src/web`.
- No emojis anywhere.

---

## File Structure

**Gateway**
- `src/gateway/Governance/GovernanceOptions.cs` — approval flag configuration section.
- `src/gateway/Governance/IApprovalFlagStore.cs` — runtime flag contract.
- `src/gateway/Governance/InMemoryApprovalFlagStore.cs` — in-memory flag implementation.
- `src/gateway/Nlp/Guardrails/ReportIntake.cs` — collected intake record.
- `src/gateway/Nlp/Guardrails/AccessRequest.cs` — gains trailing `Intake`.
- `src/gateway/Nlp/Guardrails/IAccessRequestStore.cs` — gains `UpdateAsync`.
- `src/gateway/Nlp/Guardrails/InMemoryAccessRequestStore.cs` — implements `UpdateAsync`.
- `src/gateway/Nlp/Guardrails/GuardrailEvaluator.cs` — consults the approval flag.
- `src/gateway/Nlp/Orchestrator/NlpOrchestrator.cs` — surfaces sensitive fields on allowed results.
- `src/gateway/Reports/ReportsOptions.cs` — demo fallback configuration section.
- `src/gateway/Reports/ListingRow.cs` — demo row shape.
- `src/gateway/Reports/DemoListingSource.cs` — deterministic rows.
- `src/gateway/Reports/CsvExporter.cs` — in-memory CSV.
- `src/gateway/Reports/DemoReportPipeline.cs` — demo pipeline from utterance.
- `src/gateway/Reports/NotificationMessage.cs` — notification payload.
- `src/gateway/Reports/INotificationSender.cs` — notification contract.
- `src/gateway/Reports/SimulatedNotificationSender.cs` — recorded simulation.
- `src/gateway/Reports/ReportService.cs` — ready/blocked decision plus CSV build.
- `src/gateway/Conversations/ConversationStep.cs` — step enum.
- `src/gateway/Conversations/ConversationControl.cs` — control enum.
- `src/gateway/Conversations/ReportIntakeDraft.cs` — mid-flow draft.
- `src/gateway/Conversations/ConversationState.cs` — persisted conversation state.
- `src/gateway/Conversations/IConversationStore.cs` — conversation store contract.
- `src/gateway/Conversations/InMemoryConversationStore.cs` — in-memory store.
- `src/gateway/Conversations/ConversationAnswer.cs` — answer payload.
- `src/gateway/Conversations/ColumnOption.cs` — column with selection.
- `src/gateway/Conversations/ConversationTurn.cs` — turn response.
- `src/gateway/Conversations/ConversationStartRequest.cs` — start payload.
- `src/gateway/Conversations/IColumnCatalog.cs` — column catalog contract.
- `src/gateway/Conversations/ColumnCatalog.cs` — standard plus query columns.
- `src/gateway/Conversations/ConversationOrchestrator.cs` — the step machine.
- `src/gateway/Nlp/NlpServiceCollectionExtensions.cs` — DI wiring.
- `src/gateway/Program.cs` — new endpoints.

**Gateway tests**
- `tests/Gateway.Tests/Governance/ApprovalFlagStoreTests.cs`
- `tests/Gateway.Tests/Nlp/GuardrailEvaluatorFlagTests.cs`
- `tests/Gateway.Tests/Nlp/ReportIntakeTests.cs`
- `tests/Gateway.Tests/Reports/DemoListingSourceTests.cs`
- `tests/Gateway.Tests/Reports/CsvExporterTests.cs`
- `tests/Gateway.Tests/Reports/NotificationSenderTests.cs`
- `tests/Gateway.Tests/Reports/ReportServiceTests.cs`
- `tests/Gateway.Tests/Reports/DemoReportPipelineTests.cs`
- `tests/Gateway.Tests/Nlp/GuardrailTestFactory.cs` — updated for the flag store.
- `tests/Gateway.Tests/Conversations/ConversationStoreTests.cs`
- `tests/Gateway.Tests/Conversations/ColumnCatalogTests.cs`
- `tests/Gateway.Tests/Conversations/ConversationOrchestratorTests.cs`
- `tests/Gateway.Tests/ApiConversationTests.cs`

**SPA**
- `src/web/src/app/models/conversation.ts` — conversation models.
- `src/web/src/app/services/conversation.service.ts` — conversation HTTP calls.
- `src/web/src/app/services/conversation.service.spec.ts`
- `src/web/src/app/chat/chat-thread.component.ts`
- `src/web/src/app/chat/chat-thread.component.html`
- `src/web/src/app/chat/chat-thread.component.scss`
- `src/web/src/app/chat/chat-thread.component.spec.ts`
- `src/web/src/app/governance/governance-toggle.component.ts`
- `src/web/src/app/governance/governance-toggle.component.spec.ts`
- `src/web/src/app/services/session.service.ts` — add `role()`.
- `src/web/src/app/services/session.service.spec.ts` — role test.
- `src/web/src/app/app.component.ts` and `.html` — host the chat and right rail.
- `src/web/src/app/app.component.spec.ts` — updated.

---

### Task 1: Governance approval flag store

**Files:**
- Create: `src/gateway/Governance/GovernanceOptions.cs`
- Create: `src/gateway/Governance/IApprovalFlagStore.cs`
- Create: `src/gateway/Governance/InMemoryApprovalFlagStore.cs`
- Test: `tests/Gateway.Tests/Governance/ApprovalFlagStoreTests.cs`

**Interfaces:**
- Produces: `GovernanceOptions.SectionName`, `GovernanceOptions.ApprovalEnabled`, `IApprovalFlagStore.Enabled`, `IApprovalFlagStore.SetAsync(bool, CancellationToken)`, `InMemoryApprovalFlagStore(bool)`.

- [ ] **Step 1: Write the failing test**

```csharp
using Gateway.Governance;

namespace Gateway.Tests.Governance;

public class ApprovalFlagStoreTests
{
    [Fact]
    public void Defaults_to_the_initial_value()
    {
        Assert.True(new InMemoryApprovalFlagStore(true).Enabled);
        Assert.False(new InMemoryApprovalFlagStore(false).Enabled);
    }

    [Fact]
    public async Task Set_flips_the_value_at_runtime()
    {
        var store = new InMemoryApprovalFlagStore(true);

        await store.SetAsync(false);

        Assert.False(store.Enabled);

        await store.SetAsync(true);

        Assert.True(store.Enabled);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ApprovalFlagStoreTests"`
Expected: FAIL with "The type or namespace name 'Governance' could not be found".

- [ ] **Step 3: Write the implementation**

`src/gateway/Governance/GovernanceOptions.cs`
```csharp
namespace Gateway.Governance;

public sealed class GovernanceOptions
{
    public const string SectionName = "Governance";

    public bool ApprovalEnabled { get; set; } = true;
}
```

`src/gateway/Governance/IApprovalFlagStore.cs`
```csharp
namespace Gateway.Governance;

public interface IApprovalFlagStore
{
    bool Enabled { get; }

    Task<bool> SetAsync(bool enabled, CancellationToken cancellationToken = default);
}
```

`src/gateway/Governance/InMemoryApprovalFlagStore.cs`
```csharp
namespace Gateway.Governance;

public sealed class InMemoryApprovalFlagStore : IApprovalFlagStore
{
    private int _enabled;

    public InMemoryApprovalFlagStore(bool initialEnabled = true)
    {
        _enabled = initialEnabled ? 1 : 0;
    }

    public bool Enabled => Volatile.Read(ref _enabled) == 1;

    public Task<bool> SetAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        Volatile.Write(ref _enabled, enabled ? 1 : 0);
        return Task.FromResult(enabled);
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ApprovalFlagStoreTests"`
Expected: PASS, 2 tests.

- [ ] **Step 5: Commit**

```bash
git add src/gateway/Governance tests/Gateway.Tests/Governance
git commit -m "feat(governance): add runtime approval flag store

Co-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>"
```

---

### Task 2: Guardrail consults the approval flag

**Files:**
- Modify: `src/gateway/Nlp/Guardrails/GuardrailEvaluator.cs`
- Modify: `src/gateway/Nlp/Orchestrator/NlpOrchestrator.cs`
- Modify: `tests/Gateway.Tests/Nlp/GuardrailTestFactory.cs`
- Test: `tests/Gateway.Tests/Nlp/GuardrailEvaluatorFlagTests.cs`

**Interfaces:**
- Consumes: `IApprovalFlagStore` from Task 1.
- Produces: `GuardrailEvaluator(SchemaWhitelist, ISensitiveFieldRegistry, IApprovalFlagStore)`; allowed results carry `SensitiveFields`; `NlpOrchestrator` sets `SensitiveFields` on allowed results too.

- [ ] **Step 1: Write the failing test**

```csharp
using Gateway.Governance;
using Gateway.Nlp.Guardrails;

namespace Gateway.Tests.Nlp;

public class GuardrailEvaluatorFlagTests
{
    private const string CoordinatesPipeline =
        """[{"$match":{"address.location.coordinates.lat":{"$gte":40}}}]""";

    [Fact]
    public void Pauses_sensitive_approval_fields_when_the_flag_is_on()
    {
        var evaluator = GuardrailTestFactory.FromAssets(new InMemoryApprovalFlagStore(true));

        var result = evaluator.Evaluate(CoordinatesPipeline);

        Assert.Equal(GuardrailOutcome.PausedForApproval, result.Outcome);
        Assert.Contains("address.location.coordinates", result.SensitiveFields);
    }

    [Fact]
    public void Allows_but_reports_sensitive_fields_when_the_flag_is_off()
    {
        var evaluator = GuardrailTestFactory.FromAssets(new InMemoryApprovalFlagStore(false));

        var result = evaluator.Evaluate(CoordinatesPipeline);

        Assert.Equal(GuardrailOutcome.Allowed, result.Outcome);
        Assert.Contains("address.location.coordinates", result.SensitiveFields);
    }

    [Fact]
    public void Still_rejects_write_operators_when_the_flag_is_off()
    {
        var evaluator = GuardrailTestFactory.FromAssets(new InMemoryApprovalFlagStore(false));

        var result = evaluator.Evaluate("""[{"$out":"archive"}]""");

        Assert.Equal(GuardrailOutcome.Rejected, result.Outcome);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~GuardrailEvaluatorFlagTests"`
Expected: FAIL to compile because `GuardrailTestFactory.FromAssets` does not take a flag store.

- [ ] **Step 3: Update the evaluator, factory, and orchestrator**

`src/gateway/Nlp/Guardrails/GuardrailEvaluator.cs`
```csharp
using Gateway.Governance;

namespace Gateway.Nlp.Guardrails;

public sealed class GuardrailEvaluator
{
    private readonly SchemaWhitelist _whitelist;
    private readonly ISensitiveFieldRegistry _registry;
    private readonly IApprovalFlagStore _flags;

    public GuardrailEvaluator(SchemaWhitelist whitelist, ISensitiveFieldRegistry registry, IApprovalFlagStore flags)
    {
        _whitelist = whitelist;
        _registry = registry;
        _flags = flags;
    }

    public GuardrailResult Evaluate(string? pipelineJson)
    {
        var analysis = MqlAnalyzer.Analyze(pipelineJson);

        var violations = ReadOnlyRule.FindViolations(analysis);
        if (violations.Count > 0)
        {
            return new GuardrailResult(
                GuardrailOutcome.Rejected,
                analysis.IsParsable
                    ? $"read-only violation: {string.Join(", ", violations)}"
                    : analysis.ParseError,
                [],
                [],
                violations);
        }

        var unknownFields = _whitelist
            .FindUnknown(analysis.FieldPaths)
            .Where(path => _registry.Match([path]).Count == 0)
            .ToList();

        if (unknownFields.Count > 0)
        {
            return new GuardrailResult(
                GuardrailOutcome.Rejected,
                $"unknown field path: {string.Join(", ", unknownFields)}",
                [],
                unknownFields,
                []);
        }

        var matched = _registry
            .Match(analysis.FieldPaths)
            .Where(flag => flag.IsSensitive)
            .ToList();

        var sensitiveFields = matched
            .Select(flag => flag.Path)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var approvalFields = matched
            .Where(flag => flag.RequiresApproval)
            .Select(flag => flag.Path)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (approvalFields.Count > 0 && _flags.Enabled)
        {
            return new GuardrailResult(
                GuardrailOutcome.PausedForApproval,
                $"sensitive field requires approval: {string.Join(", ", approvalFields)}",
                approvalFields,
                [],
                []);
        }

        return new GuardrailResult(GuardrailOutcome.Allowed, null, sensitiveFields, [], []);
    }
}
```

`tests/Gateway.Tests/Nlp/GuardrailTestFactory.cs`
```csharp
using Gateway.Governance;
using Gateway.Nlp.Guardrails;

namespace Gateway.Tests.Nlp;

internal static class GuardrailTestFactory
{
    public static GuardrailEvaluator FromAssets(IApprovalFlagStore? flags = null)
    {
        return new GuardrailEvaluator(
            SchemaWhitelist.LoadFromSchemaFile(
                Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Prompts", "schema.txt")),
            InMemorySensitiveFieldRegistry.LoadFromDirectory(
                Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Schema")),
            flags ?? new InMemoryApprovalFlagStore(true));
    }
}
```

In `src/gateway/Nlp/Orchestrator/NlpOrchestrator.cs`, replace the final two lines of `OrchestrateAsync`:

```csharp
        _events.Publish(new AgentEvent("agent.completed", "completed", result.Kind.ToString()));
        return result with
        {
            SensitiveFields = guard.SensitiveFields.Count > 0 ? guard.SensitiveFields : null
        };
```

Also replace the early return when `result.Mql is null` so it stays unchanged (no edit needed there).

- [ ] **Step 4: Run the guardrail and orchestrator tests**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~Guardrail|FullyQualifiedName~NlpOrchestrator"`
Expected: PASS. If a pre-existing test constructs `GuardrailEvaluator` directly, add `new InMemoryApprovalFlagStore(true)` as the third argument.

- [ ] **Step 5: Commit**

```bash
git add src/gateway/Nlp/Guardrails/GuardrailEvaluator.cs src/gateway/Nlp/Orchestrator/NlpOrchestrator.cs tests/Gateway.Tests/Nlp
git commit -m "feat(guardrails): gate approval pause on the runtime flag

Co-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>"
```

---

### Task 3: Report intake on the access request

**Files:**
- Create: `src/gateway/Nlp/Guardrails/ReportIntake.cs`
- Modify: `src/gateway/Nlp/Guardrails/AccessRequest.cs`
- Modify: `src/gateway/Nlp/Guardrails/IAccessRequestStore.cs`
- Modify: `src/gateway/Nlp/Guardrails/InMemoryAccessRequestStore.cs`
- Test: `tests/Gateway.Tests/Nlp/ReportIntakeTests.cs`

**Interfaces:**
- Produces: `ReportIntake(string RequesterEmail, string Purpose, string? ProjectCode, string ManagerEmail, IReadOnlyList<string> Columns, string DeliveryFormat)`, constants `ReportIntake.Email` and `ReportIntake.Csv`; `IAccessRequestStore.UpdateAsync(AccessRequest, CancellationToken)`.

- [ ] **Step 1: Write the failing test**

```csharp
using Gateway.Nlp.Guardrails;

namespace Gateway.Tests.Nlp;

public class ReportIntakeTests
{
    [Fact]
    public async Task Update_replaces_a_stored_request_with_the_intake()
    {
        var store = new InMemoryAccessRequestStore();
        var created = await store.CreateAsync(new AccessRequest(
            string.Empty,
            "sess_1",
            "[{\"$match\":{}}]",
            ["address.location.coordinates"],
            AccessRequest.PendingLead,
            null,
            DateTimeOffset.UnixEpoch));

        var intake = new ReportIntake(
            "analyst@enterprise.com",
            "Quarterly geo analysis",
            "PROJ-GEO",
            "manager@enterprise.com",
            ["name", "price"],
            ReportIntake.Csv);

        var updated = await store.UpdateAsync(created with { Intake = intake });

        var fetched = await store.GetAsync(created.Id);
        Assert.NotNull(fetched);
        Assert.NotNull(fetched!.Intake);
        Assert.Equal("manager@enterprise.com", fetched.Intake!.ManagerEmail);
        Assert.Equal("PROJ-GEO", fetched.Intake.ProjectCode);
        Assert.Equal(created.Id, updated.Id);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ReportIntakeTests"`
Expected: FAIL to compile because `ReportIntake` and `UpdateAsync` do not exist.

- [ ] **Step 3: Write the implementation**

`src/gateway/Nlp/Guardrails/ReportIntake.cs`
```csharp
namespace Gateway.Nlp.Guardrails;

public sealed record ReportIntake(
    string RequesterEmail,
    string Purpose,
    string? ProjectCode,
    string ManagerEmail,
    IReadOnlyList<string> Columns,
    string DeliveryFormat)
{
    public const string Email = "EMAIL";

    public const string Csv = "CSV";
}
```

`src/gateway/Nlp/Guardrails/AccessRequest.cs`
```csharp
namespace Gateway.Nlp.Guardrails;

public sealed record AccessRequest(
    string Id,
    string SessionId,
    string Mql,
    IReadOnlyList<string> SensitiveFields,
    string Status,
    string? Justification,
    DateTimeOffset CreatedAt,
    ReportIntake? Intake = null)
{
    public const string PendingLead = "PENDING_LEAD";
}
```

`src/gateway/Nlp/Guardrails/IAccessRequestStore.cs`
```csharp
namespace Gateway.Nlp.Guardrails;

public interface IAccessRequestStore
{
    Task<AccessRequest> CreateAsync(AccessRequest request, CancellationToken cancellationToken = default);

    Task<AccessRequest?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<AccessRequest> UpdateAsync(AccessRequest request, CancellationToken cancellationToken = default);
}
```

Append to `src/gateway/Nlp/Guardrails/InMemoryAccessRequestStore.cs` before the closing brace:
```csharp
    public Task<AccessRequest> UpdateAsync(AccessRequest request, CancellationToken cancellationToken = default)
    {
        _requests[request.Id] = request;
        return Task.FromResult(request);
    }
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ReportIntakeTests|FullyQualifiedName~AccessRequestStore"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/gateway/Nlp/Guardrails tests/Gateway.Tests/Nlp/ReportIntakeTests.cs
git commit -m "feat(guardrails): attach report intake to access requests

Co-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>"
```

---

### Task 4: Column catalog

**Files:**
- Create: `src/gateway/Conversations/ColumnOption.cs`
- Create: `src/gateway/Conversations/IColumnCatalog.cs`
- Create: `src/gateway/Conversations/ColumnCatalog.cs`
- Test: `tests/Gateway.Tests/Conversations/ColumnCatalogTests.cs`

**Interfaces:**
- Consumes: `MqlAnalyzer.Analyze(string?)` and `MqlAnalysis.FieldPaths`.
- Produces: `ColumnOption(string Name, bool Selected)`, `IColumnCatalog.Available(string? mql)`, `ColumnCatalog.Standard`.

- [ ] **Step 1: Write the failing test**

```csharp
using Gateway.Conversations;

namespace Gateway.Tests.Conversations;

public class ColumnCatalogTests
{
    private readonly ColumnCatalog _catalog = new();

    [Fact]
    public void Standard_columns_come_first_and_are_selected()
    {
        var columns = _catalog.Available(null);

        Assert.Equal(6, columns.Count);
        Assert.Equal("name", columns[0].Name);
        Assert.All(columns, column => Assert.True(column.Selected));
    }

    [Fact]
    public void Query_fields_are_appended_without_duplicates()
    {
        var mql = """[{"$match":{"security_deposit":{"$lte":100},"address.market":"New York"}}]""";

        var names = _catalog.Available(mql).Select(column => column.Name).ToList();

        Assert.Contains("security_deposit", names);
        Assert.Single(names.Where(name => name == "address.market"));
        Assert.Equal("name", names[0]);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ColumnCatalogTests"`
Expected: FAIL to compile because `ColumnCatalog` does not exist.

- [ ] **Step 3: Write the implementation**

`src/gateway/Conversations/ColumnOption.cs`
```csharp
namespace Gateway.Conversations;

public sealed record ColumnOption(string Name, bool Selected);
```

`src/gateway/Conversations/IColumnCatalog.cs`
```csharp
namespace Gateway.Conversations;

public interface IColumnCatalog
{
    IReadOnlyList<ColumnOption> Available(string? mql);
}
```

`src/gateway/Conversations/ColumnCatalog.cs`
```csharp
using Gateway.Nlp.Guardrails;

namespace Gateway.Conversations;

public sealed class ColumnCatalog : IColumnCatalog
{
    public static readonly IReadOnlyList<string> Standard =
    [
        "name",
        "address.market",
        "price",
        "room_type",
        "accommodates",
        "review_scores.rating"
    ];

    public IReadOnlyList<ColumnOption> Available(string? mql)
    {
        var names = new List<string>(Standard);
        var seen = new HashSet<string>(Standard, StringComparer.Ordinal);

        foreach (var path in MqlAnalyzer.Analyze(mql).FieldPaths)
        {
            if (seen.Add(path))
            {
                names.Add(path);
            }
        }

        return names.Select(name => new ColumnOption(name, true)).ToList();
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ColumnCatalogTests"`
Expected: PASS, 2 tests.

- [ ] **Step 5: Commit**

```bash
git add src/gateway/Conversations tests/Gateway.Tests/Conversations/ColumnCatalogTests.cs
git commit -m "feat(conversations): derive report columns from query and defaults

Co-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>"
```

---

### Task 5: Demo rows and CSV export

**Files:**
- Create: `src/gateway/Reports/ListingRow.cs`
- Create: `src/gateway/Reports/DemoListingSource.cs`
- Create: `src/gateway/Reports/CsvExporter.cs`
- Test: `tests/Gateway.Tests/Reports/DemoListingSourceTests.cs`
- Test: `tests/Gateway.Tests/Reports/CsvExporterTests.cs`

**Interfaces:**
- Produces: `ListingRow`, `DemoListingSource.Rows()`, `CsvExporter.Export(IReadOnlyList<ListingRow>, IReadOnlyList<string>)`.

- [ ] **Step 1: Write the failing tests**

`tests/Gateway.Tests/Reports/DemoListingSourceTests.cs`
```csharp
using Gateway.Reports;

namespace Gateway.Tests.Reports;

public class DemoListingSourceTests
{
    [Fact]
    public void Rows_are_deterministic_and_multi_market()
    {
        var first = DemoListingSource.Rows();
        var second = DemoListingSource.Rows();

        Assert.Equal(24, first.Count);
        Assert.True(first.Select(row => row.Market).Distinct().Count() >= 3);
        Assert.Equal(first[0].Name, second[0].Name);
        Assert.Equal(first[23].Price, second[23].Price);
    }
}
```

`tests/Gateway.Tests/Reports/CsvExporterTests.cs`
```csharp
using Gateway.Reports;

namespace Gateway.Tests.Reports;

public class CsvExporterTests
{
    [Fact]
    public void Exports_only_the_requested_columns_with_a_header()
    {
        var rows = new List<ListingRow>
        {
            new("1", "Sunny \"Loft\"", "New York", 210m, "Entire home", 3, 4.8, "40.7", "-74.0")
        };

        var csv = CsvExporter.Export(rows, ["name", "price"]);

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("name,price", lines[0]);
        Assert.Equal("\"Sunny \"\"Loft\"\"\",210", lines[1]);
    }

    [Fact]
    public void Unknown_columns_export_as_empty_values()
    {
        var rows = new List<ListingRow>
        {
            new("1", "Sunny Loft", "New York", 210m, "Entire home", 3, 4.8, null, null)
        };

        var csv = CsvExporter.Export(rows, ["name", "security_deposit"]);

        Assert.Equal("name,security_deposit\nSunny Loft,", csv);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~DemoListingSourceTests|FullyQualifiedName~CsvExporterTests"`
Expected: FAIL to compile because `DemoListingSource` and `CsvExporter` do not exist.

- [ ] **Step 3: Write the implementation**

`src/gateway/Reports/ListingRow.cs`
```csharp
namespace Gateway.Reports;

public sealed record ListingRow(
    string Id,
    string Name,
    string Market,
    decimal Price,
    string RoomType,
    int Accommodates,
    double Rating,
    string? Latitude,
    string? Longitude);
```

`src/gateway/Reports/DemoListingSource.cs`
```csharp
namespace Gateway.Reports;

public static class DemoListingSource
{
    private static readonly string[] Markets = ["New York", "Los Angeles", "Sydney"];

    public static IReadOnlyList<ListingRow> Rows()
    {
        var rows = new List<ListingRow>(24);

        for (var index = 0; index < 24; index++)
        {
            var market = Markets[index % Markets.Length];
            rows.Add(new ListingRow(
                $"lst_{index + 1:D3}",
                $"Demo Listing {index + 1} in {market}",
                market,
                90m + (index * 15m),
                index % 2 == 0 ? "Entire home/apt" : "Private room",
                2 + (index % 5),
                4.0 + ((index % 10) / 10.0),
                (40.70 + (index * 0.01)).ToString("0.00"),
                (-74.00 - (index * 0.01)).ToString("0.00")));
        }

        return rows;
    }
}
```

`src/gateway/Reports/CsvExporter.cs`
```csharp
using System.Globalization;
using System.Text;

namespace Gateway.Reports;

public static class CsvExporter
{
    private static readonly Dictionary<string, Func<ListingRow, string>> Selectors = new(StringComparer.Ordinal)
    {
        ["name"] = row => row.Name,
        ["address.market"] = row => row.Market,
        ["price"] = row => row.Price.ToString(CultureInfo.InvariantCulture),
        ["room_type"] = row => row.RoomType,
        ["accommodates"] = row => row.Accommodates.ToString(CultureInfo.InvariantCulture),
        ["review_scores.rating"] = row => row.Rating.ToString("0.0", CultureInfo.InvariantCulture),
        ["address.location.coordinates"] = row =>
            row.Latitude is null ? string.Empty : $"{row.Latitude},{row.Longitude}"
    };

    public static string Export(IReadOnlyList<ListingRow> rows, IReadOnlyList<string> columns)
    {
        var builder = new StringBuilder();
        builder.Append(string.Join(',', columns.Select(Escape)));

        foreach (var row in rows)
        {
            builder.Append('\n');
            builder.Append(string.Join(',', columns.Select(column => Escape(Value(row, column)))));
        }

        return builder.ToString();
    }

    private static string Value(ListingRow row, string column)
    {
        return Selectors.TryGetValue(column, out var selector) ? selector(row) : string.Empty;
    }

    private static string Escape(string value)
    {
        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~DemoListingSourceTests|FullyQualifiedName~CsvExporterTests"`
Expected: PASS, 3 tests.

- [ ] **Step 5: Commit**

```bash
git add src/gateway/Reports tests/Gateway.Tests/Reports
git commit -m "feat(reports): add demo rows and in-memory csv export

Co-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>"
```

---

### Task 6: Simulated notifications, report service, and demo pipeline

**Files:**
- Create: `src/gateway/Reports/ReportsOptions.cs`
- Create: `src/gateway/Reports/NotificationMessage.cs`
- Create: `src/gateway/Reports/INotificationSender.cs`
- Create: `src/gateway/Reports/SimulatedNotificationSender.cs`
- Create: `src/gateway/Reports/DemoReportPipeline.cs`
- Create: `src/gateway/Reports/ReportService.cs`
- Test: `tests/Gateway.Tests/Reports/NotificationSenderTests.cs`
- Test: `tests/Gateway.Tests/Reports/DemoReportPipelineTests.cs`
- Test: `tests/Gateway.Tests/Reports/ReportServiceTests.cs`

**Interfaces:**
- Produces: `ReportsOptions.SectionName`, `ReportsOptions.DemoFallbackEnabled`, `NotificationMessage`, `INotificationSender.SendAsync`, `INotificationSender.Sent`, `SimulatedNotificationSender`, `DemoReportPipeline.FromUtterance(string)`, `ReportService.BuildCsv(IReadOnlyList<string>, bool)` returning `ReportBuild(bool Ready, string? Csv, string? Reason)`.

- [ ] **Step 1: Write the failing tests**

`tests/Gateway.Tests/Reports/NotificationSenderTests.cs`
```csharp
using Gateway.Reports;

namespace Gateway.Tests.Reports;

public class NotificationSenderTests
{
    [Fact]
    public async Task Records_sent_messages_in_order()
    {
        var sender = new SimulatedNotificationSender();

        await sender.SendAsync(new NotificationMessage("report_email", "a@x.com", "subject", "body"));
        await sender.SendAsync(new NotificationMessage("manager_notification", "m@x.com", "subject", "body"));

        Assert.Equal(2, sender.Sent.Count);
        Assert.Equal("report_email", sender.Sent[0].Kind);
        Assert.Equal("m@x.com", sender.Sent[1].Recipient);
    }
}
```

`tests/Gateway.Tests/Reports/DemoReportPipelineTests.cs`
```csharp
using Gateway.Nlp.Guardrails;
using Gateway.Reports;

namespace Gateway.Tests.Reports;

public class DemoReportPipelineTests
{
    [Fact]
    public void Produces_a_read_only_parsable_pipeline()
    {
        var pipeline = DemoReportPipeline.FromUtterance("average price by market");

        var analysis = MqlAnalyzer.Analyze(pipeline);

        Assert.True(analysis.IsParsable);
        Assert.Empty(ReadOnlyRule.FindViolations(analysis));
    }
}
```

`tests/Gateway.Tests/Reports/ReportServiceTests.cs`
```csharp
using Gateway.Reports;

namespace Gateway.Tests.Reports;

public class ReportServiceTests
{
    [Fact]
    public void Builds_csv_for_confirmed_columns()
    {
        var build = ReportService.BuildCsv(["name", "price"], awaitingApproval: false);

        Assert.True(build.Ready);
        Assert.StartsWith("name,price\n", build.Csv);
    }

    [Fact]
    public void Blocks_csv_while_awaiting_approval()
    {
        var build = ReportService.BuildCsv(["name"], awaitingApproval: true);

        Assert.False(build.Ready);
        Assert.Contains("awaiting approval", build.Reason);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~NotificationSenderTests|FullyQualifiedName~DemoReportPipelineTests|FullyQualifiedName~ReportServiceTests"`
Expected: FAIL to compile because the reports types do not exist.

- [ ] **Step 3: Write the implementation**

`src/gateway/Reports/ReportsOptions.cs`
```csharp
namespace Gateway.Reports;

public sealed class ReportsOptions
{
    public const string SectionName = "Reports";

    public bool DemoFallbackEnabled { get; set; } = true;
}
```

`src/gateway/Reports/NotificationMessage.cs`
```csharp
namespace Gateway.Reports;

public sealed record NotificationMessage(string Kind, string Recipient, string Subject, string Body);
```

`src/gateway/Reports/INotificationSender.cs`
```csharp
namespace Gateway.Reports;

public interface INotificationSender
{
    IReadOnlyList<NotificationMessage> Sent { get; }

    Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}
```

`src/gateway/Reports/SimulatedNotificationSender.cs`
```csharp
using System.Collections.Concurrent;

namespace Gateway.Reports;

public sealed class SimulatedNotificationSender : INotificationSender
{
    private readonly ConcurrentQueue<NotificationMessage> _sent = new();

    public IReadOnlyList<NotificationMessage> Sent => _sent.ToArray();

    public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }
}
```

`src/gateway/Reports/DemoReportPipeline.cs`
```csharp
namespace Gateway.Reports;

public static class DemoReportPipeline
{
    public static string FromUtterance(string utterance)
    {
        return """[{"$match":{"address.market":"New York"}},{"$group":{"_id":"$address.market","avg_price":{"$avg":"$price"},"count":{"$sum":1}}},{"$sort":{"avg_price":-1}},{"$limit":10}]""";
    }
}
```

`src/gateway/Reports/ReportService.cs`
```csharp
namespace Gateway.Reports;

public sealed record ReportBuild(bool Ready, string? Csv, string? Reason);

public static class ReportService
{
    public static ReportBuild BuildCsv(IReadOnlyList<string> columns, bool awaitingApproval)
    {
        if (awaitingApproval)
        {
            return new ReportBuild(
                false,
                null,
                "This report is awaiting approval and cannot be downloaded yet.");
        }

        if (columns.Count == 0)
        {
            return new ReportBuild(false, null, "No columns were confirmed for this report.");
        }

        return new ReportBuild(true, CsvExporter.Export(DemoListingSource.Rows(), columns), null);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~NotificationSenderTests|FullyQualifiedName~DemoReportPipelineTests|FullyQualifiedName~ReportServiceTests"`
Expected: PASS, 4 tests.

- [ ] **Step 5: Commit**

```bash
git add src/gateway/Reports tests/Gateway.Tests/Reports
git commit -m "feat(reports): add simulated notifications and report delivery

Co-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>"
```

---

### Task 7: Conversation state and store

**Files:**
- Create: `src/gateway/Conversations/ConversationStep.cs`
- Create: `src/gateway/Conversations/ConversationControl.cs`
- Create: `src/gateway/Conversations/ReportIntakeDraft.cs`
- Create: `src/gateway/Conversations/ConversationState.cs`
- Create: `src/gateway/Conversations/IConversationStore.cs`
- Create: `src/gateway/Conversations/InMemoryConversationStore.cs`
- Test: `tests/Gateway.Tests/Conversations/ConversationStoreTests.cs`

**Interfaces:**
- Consumes: `NlpRouteKind`, `NlpQueryResponse`.
- Produces: `ConversationStep`, `ConversationControl`, `ReportIntakeDraft`, `ConversationState`, `IConversationStore`, `InMemoryConversationStore`.

- [ ] **Step 1: Write the failing test**

```csharp
using Gateway.Conversations;
using Gateway.Nlp.Http;
using Gateway.Nlp.Router;

namespace Gateway.Tests.Conversations;

public class ConversationStoreTests
{
    [Fact]
    public async Task Creates_reads_and_updates_a_conversation()
    {
        var store = new InMemoryConversationStore();
        var state = new ConversationState(
            string.Empty,
            "sess_1",
            ConversationStep.Email,
            "listings with pools",
            "[{\"$match\":{}}]",
            NlpRouteKind.ComplexLlmRequired,
            null,
            [],
            false,
            false,
            new ReportIntakeDraft(),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);

        var created = await store.CreateAsync(state);
        Assert.False(string.IsNullOrWhiteSpace(created.Id));

        var fetched = await store.GetAsync(created.Id);
        Assert.Equal(ConversationStep.Email, fetched!.Step);

        await store.UpdateAsync(fetched with { Step = ConversationStep.Purpose });

        var updated = await store.GetAsync(created.Id);
        Assert.Equal(ConversationStep.Purpose, updated!.Step);
    }

    [Fact]
    public async Task Missing_ids_return_null()
    {
        var store = new InMemoryConversationStore();

        Assert.Null(await store.GetAsync("missing"));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ConversationStoreTests"`
Expected: FAIL to compile because the conversation types do not exist.

- [ ] **Step 3: Write the implementation**

`src/gateway/Conversations/ConversationStep.cs`
```csharp
namespace Gateway.Conversations;

public enum ConversationStep
{
    Email,
    Purpose,
    ManagerEmail,
    Columns,
    Delivery,
    Complete
}
```

`src/gateway/Conversations/ConversationControl.cs`
```csharp
namespace Gateway.Conversations;

public enum ConversationControl
{
    None,
    Email,
    Purpose,
    Columns,
    Delivery
}
```

`src/gateway/Conversations/ReportIntakeDraft.cs`
```csharp
namespace Gateway.Conversations;

public sealed record ReportIntakeDraft(
    string? RequesterEmail = null,
    string? Purpose = null,
    string? ProjectCode = null,
    string? ManagerEmail = null,
    IReadOnlyList<string>? Columns = null,
    string? DeliveryFormat = null);
```

`src/gateway/Conversations/ConversationState.cs`
```csharp
using Gateway.Nlp.Http;
using Gateway.Nlp.Router;

namespace Gateway.Conversations;

public sealed record ConversationState(
    string Id,
    string SessionId,
    ConversationStep Step,
    string Utterance,
    string? Mql,
    NlpRouteKind Kind,
    string? AccessRequestId,
    IReadOnlyList<string> SensitiveFields,
    bool DemoReport,
    bool ApprovalRequired,
    ReportIntakeDraft Draft,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? Message = null,
    NlpQueryResponse? Result = null);
```

`src/gateway/Conversations/IConversationStore.cs`
```csharp
namespace Gateway.Conversations;

public interface IConversationStore
{
    Task<ConversationState> CreateAsync(ConversationState state, CancellationToken cancellationToken = default);

    Task<ConversationState?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<ConversationState> UpdateAsync(ConversationState state, CancellationToken cancellationToken = default);
}
```

`src/gateway/Conversations/InMemoryConversationStore.cs`
```csharp
using System.Collections.Concurrent;

namespace Gateway.Conversations;

public sealed class InMemoryConversationStore : IConversationStore
{
    private readonly ConcurrentDictionary<string, ConversationState> _states = new(StringComparer.Ordinal);

    public Task<ConversationState> CreateAsync(ConversationState state, CancellationToken cancellationToken = default)
    {
        var stored = string.IsNullOrWhiteSpace(state.Id)
            ? state with { Id = Guid.NewGuid().ToString("N") }
            : state;

        _states[stored.Id] = stored;
        return Task.FromResult(stored);
    }

    public Task<ConversationState?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        _states.TryGetValue(id, out var state);
        return Task.FromResult(state);
    }

    public Task<ConversationState> UpdateAsync(ConversationState state, CancellationToken cancellationToken = default)
    {
        _states[state.Id] = state;
        return Task.FromResult(state);
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ConversationStoreTests"`
Expected: PASS, 2 tests.

- [ ] **Step 5: Commit**

```bash
git add src/gateway/Conversations tests/Gateway.Tests/Conversations
git commit -m "feat(conversations): add conversation state and in-memory store

Co-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>"
```

---

### Task 8: Conversation turn types and step machine

**Files:**
- Create: `src/gateway/Conversations/ConversationAnswer.cs`
- Create: `src/gateway/Conversations/ConversationTurn.cs`
- Create: `src/gateway/Conversations/ConversationStartRequest.cs`
- Create: `src/gateway/Conversations/ConversationOrchestrator.cs`
- Test: `tests/Gateway.Tests/Conversations/ConversationOrchestratorTests.cs`

**Interfaces:**
- Consumes: `INlpOrchestrator`, `IConversationStore`, `IAccessRequestStore`, `IApprovalFlagStore`, `INotificationSender`, `IColumnCatalog`, `IAgentEventSink`, `IOptions<ReportsOptions>`, `TimeProvider`.
- Produces: `ConversationAnswer`, `ConversationTurn`, `ConversationStartRequest`, `ConversationOrchestrator.StartAsync`, `AnswerAsync`, `GetAsync`.

- [ ] **Step 1: Write the failing test**

```csharp
using Gateway.Conversations;
using Gateway.Governance;
using Gateway.Nlp.Http;
using Gateway.Nlp.Orchestrator;
using Gateway.Nlp.Router;
using Gateway.Reports;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Conversations;

public class ConversationOrchestratorTests
{
    private sealed class StubOrchestrator : INlpOrchestrator
    {
        private readonly NlpRouteResult _result;

        public StubOrchestrator(NlpRouteResult result) => _result = result;

        public Task<NlpRouteResult> OrchestrateAsync(string utterance, string sessionId = "anonymous", CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }

    private static NlpRouteResult Report(string mql) => new(
        NlpRouteKind.ComplexLlmRequired,
        mql,
        null,
        false,
        false,
        Gateway.Nlp.Intent.IntentKind.Search,
        false,
        new Gateway.Nlp.Mql.MqlDefaults(),
        0,
        1,
        null,
        null,
        null,
        null);

    private static ConversationOrchestrator Build(INlpOrchestrator nlp, INotificationSender sender)
    {
        return new ConversationOrchestrator(
            nlp,
            new InMemoryConversationStore(),
            new InMemoryAccessRequestStore(),
            new InMemoryApprovalFlagStore(true),
            sender,
            new ColumnCatalog(),
            new InMemoryAgentEventSink(),
            Options.Create(new ReportsOptions()),
            TimeProvider.System);
    }

    [Fact]
    public async Task Report_query_starts_at_the_email_step()
    {
        var sut = Build(new StubOrchestrator(Report("""[{"$group":{"_id":"$address.market"}}]""")), new SimulatedNotificationSender());

        var turn = await sut.StartAsync("average price by market", "sess_1", "analyst@enterprise.com");

        Assert.Equal("Email", turn.Step);
        Assert.Equal("email", turn.Control);
        Assert.Equal("analyst@enterprise.com", turn.EmailPrefill);
        Assert.Equal("ComplexLlmRequired", turn.Kind);
    }

    [Fact]
    public async Task Invalid_email_does_not_advance()
    {
        var sut = Build(new StubOrchestrator(Report("""[{"$group":{"_id":"$address.market"}}]""")), new SimulatedNotificationSender());
        var turn = await sut.StartAsync("average price by market", "sess_1", null);

        var next = await sut.AnswerAsync(turn.ConversationId, new ConversationAnswer(Email: "not-an-email"));

        Assert.Equal("Email", next!.Step);
        Assert.NotNull(next.ValidationError);
    }

    [Fact]
    public async Task Valid_steps_reach_completion_and_build_a_csv_report()
    {
        var sender = new SimulatedNotificationSender();
        var sut = Build(new StubOrchestrator(Report("""[{"$group":{"_id":"$address.market"}}]""")), sender);

        var email = await sut.StartAsync("average price by market", "sess_1", null);
        var purpose = await sut.AnswerAsync(email.ConversationId, new ConversationAnswer(Email: "analyst@enterprise.com"));
        var manager = await sut.AnswerAsync(email.ConversationId, new ConversationAnswer(Purpose: "Quarterly review", ProjectCode: "PROJ-1"));
        var columns = await sut.AnswerAsync(email.ConversationId, new ConversationAnswer(ManagerEmail: "manager@enterprise.com"));
        var delivery = await sut.AnswerAsync(email.ConversationId, new ConversationAnswer(Columns: ["name", "price"]));
        var complete = await sut.AnswerAsync(email.ConversationId, new ConversationAnswer(Delivery: ReportIntake.Csv));

        Assert.Equal("Purpose", purpose!.Step);
        Assert.Equal("ManagerEmail", manager!.Step);
        Assert.Equal("Columns", columns!.Step);
        Assert.Equal("Delivery", delivery!.Step);
        Assert.Equal("Complete", complete!.Step);
        Assert.False(complete.ApprovalRequired);
        Assert.True(complete.Downloadable);
        Assert.Empty(sender.Sent);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ConversationOrchestratorTests"`
Expected: FAIL to compile because `ConversationOrchestrator` and friends do not exist.

- [ ] **Step 3: Write the implementation**

`src/gateway/Conversations/ConversationAnswer.cs`
```csharp
namespace Gateway.Conversations;

public sealed record ConversationAnswer(
    string? Text = null,
    string? Email = null,
    string? Purpose = null,
    string? ProjectCode = null,
    string? ManagerEmail = null,
    IReadOnlyList<string>? Columns = null,
    string? Delivery = null);
```

`src/gateway/Conversations/ConversationTurn.cs`
```csharp
using Gateway.Nlp.Http;

namespace Gateway.Conversations;

public sealed record ConversationTurn(
    string ConversationId,
    string Step,
    string Kind,
    string AssistantMessage,
    string Control,
    string? EmailPrefill,
    IReadOnlyList<ColumnOption>? Columns,
    IReadOnlyList<string> DeliveryOptions,
    string? AccessRequestId,
    bool ApprovalRequired,
    bool Downloadable,
    bool DemoReport,
    NlpQueryResponse? Result,
    string? ValidationError);
```

`src/gateway/Conversations/ConversationStartRequest.cs`
```csharp
namespace Gateway.Conversations;

public sealed record ConversationStartRequest(string? Utterance);
```

`src/gateway/Conversations/ConversationOrchestrator.cs`
```csharp
using System.Text.RegularExpressions;
using Gateway.Governance;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Http;
using Gateway.Nlp.Orchestrator;
using Gateway.Nlp.Router;
using Gateway.Reports;
using Microsoft.Extensions.Options;

namespace Gateway.Conversations;

public sealed partial class ConversationOrchestrator
{
    private readonly INlpOrchestrator _nlp;
    private readonly IConversationStore _store;
    private readonly IAccessRequestStore _accessRequests;
    private readonly IApprovalFlagStore _flags;
    private readonly INotificationSender _notifications;
    private readonly IColumnCatalog _columns;
    private readonly IAgentEventSink _events;
    private readonly ReportsOptions _reports;
    private readonly TimeProvider _clock;

    public ConversationOrchestrator(
        INlpOrchestrator nlp,
        IConversationStore store,
        IAccessRequestStore accessRequests,
        IApprovalFlagStore flags,
        INotificationSender notifications,
        IColumnCatalog columns,
        IAgentEventSink events,
        IOptions<ReportsOptions> reports,
        TimeProvider clock)
    {
        _nlp = nlp;
        _store = store;
        _accessRequests = accessRequests;
        _flags = flags;
        _notifications = notifications;
        _columns = columns;
        _events = events;
        _reports = reports.Value;
        _clock = clock;
    }

    public async Task<ConversationTurn> StartAsync(
        string utterance,
        string sessionId,
        string? requesterEmail,
        CancellationToken cancellationToken = default)
    {
        var result = await _nlp.OrchestrateAsync(utterance, sessionId, cancellationToken);

        var kind = result.Kind;
        var mql = result.Mql;
        var demo = false;

        if (kind == NlpRouteKind.ComplexLlmFailed && _reports.DemoFallbackEnabled)
        {
            kind = NlpRouteKind.ComplexLlmRequired;
            mql = DemoReportPipeline.FromUtterance(utterance);
            demo = true;
        }

        var isReport = kind is NlpRouteKind.ComplexLlmRequired or NlpRouteKind.GovernancePaused;

        if (!isReport)
        {
            var direct = new ConversationState(
                string.Empty,
                sessionId,
                ConversationStep.Complete,
                utterance,
                mql,
                kind,
                result.AccessRequestId,
                result.SensitiveFields ?? [],
                false,
                false,
                new ReportIntakeDraft(),
                _clock.GetUtcNow(),
                _clock.GetUtcNow(),
                DirectMessage(result),
                NlpQueryResponse.From(result));

            var savedDirect = await _store.CreateAsync(direct, cancellationToken);
            return ToTurn(savedDirect);
        }

        var state = new ConversationState(
            string.Empty,
            sessionId,
            ConversationStep.Email,
            utterance,
            mql,
            kind,
            result.AccessRequestId,
            result.SensitiveFields ?? [],
            demo,
            false,
            new ReportIntakeDraft(RequesterEmail: string.IsNullOrWhiteSpace(requesterEmail) ? null : requesterEmail),
            _clock.GetUtcNow(),
            _clock.GetUtcNow());

        var saved = await _store.CreateAsync(state, cancellationToken);
        _events.Publish(new AgentEvent("conversation.started", "started", saved.Id));

        return ToTurn(saved);
    }

    public async Task<ConversationTurn?> AnswerAsync(
        string conversationId,
        ConversationAnswer answer,
        CancellationToken cancellationToken = default)
    {
        var state = await _store.GetAsync(conversationId, cancellationToken);
        if (state is null)
        {
            return null;
        }

        if (state.Step == ConversationStep.Complete)
        {
            return ToTurn(state) with { ValidationError = "This conversation is already complete." };
        }

        var draft = state.Draft;

        switch (state.Step)
        {
            case ConversationStep.Email:
            {
                var email = FirstNonEmpty(answer.Email, answer.Text) ?? draft.RequesterEmail;
                if (string.IsNullOrWhiteSpace(email) || !EmailPattern().IsMatch(email.Trim()))
                {
                    return ToTurn(state) with { ValidationError = "Enter a valid email address." };
                }

                draft = draft with { RequesterEmail = email.Trim() };
                state = state with { Step = ConversationStep.Purpose };
                break;
            }

            case ConversationStep.Purpose:
            {
                var purpose = FirstNonEmpty(answer.Purpose, answer.Text);
                if (string.IsNullOrWhiteSpace(purpose) || purpose.Trim().Length < 5 || purpose.Trim().Length > 500)
                {
                    return ToTurn(state) with { ValidationError = "Describe the purpose in 5 to 500 characters." };
                }

                var projectCode = FirstNonEmpty(answer.ProjectCode, null);
                if (projectCode is not null && !ProjectCodePattern().IsMatch(projectCode))
                {
                    return ToTurn(state) with { ValidationError = "The project code may contain letters, digits, and hyphens only." };
                }

                draft = draft with { Purpose = purpose.Trim(), ProjectCode = projectCode?.Trim() };
                state = state with { Step = ConversationStep.ManagerEmail };
                break;
            }

            case ConversationStep.ManagerEmail:
            {
                var manager = FirstNonEmpty(answer.ManagerEmail, answer.Text);
                if (string.IsNullOrWhiteSpace(manager) || !EmailPattern().IsMatch(manager.Trim()))
                {
                    return ToTurn(state) with { ValidationError = "Enter a valid manager email address." };
                }

                if (string.Equals(manager.Trim(), draft.RequesterEmail, StringComparison.OrdinalIgnoreCase))
                {
                    return ToTurn(state) with { ValidationError = "The manager email must differ from your own email." };
                }

                draft = draft with { ManagerEmail = manager.Trim() };
                state = state with { Step = ConversationStep.Columns };
                break;
            }

            case ConversationStep.Columns:
            {
                var requested = answer.Columns ?? [];
                var available = _columns.Available(state.Mql).Select(option => option.Name).ToHashSet(StringComparer.Ordinal);
                if (requested.Count == 0)
                {
                    return ToTurn(state) with { ValidationError = "Select at least one column." };
                }

                if (requested.Any(column => !available.Contains(column)))
                {
                    return ToTurn(state) with { ValidationError = "One or more columns are not available for this report." };
                }

                draft = draft with { Columns = requested };
                state = state with { Step = ConversationStep.Delivery };
                break;
            }

            case ConversationStep.Delivery:
            {
                var delivery = answer.Delivery?.Trim().ToUpperInvariant();
                if (delivery is not (ReportIntake.Email or ReportIntake.Csv))
                {
                    return ToTurn(state) with { ValidationError = "Choose email delivery or CSV download." };
                }

                draft = draft with { DeliveryFormat = delivery };
                state = await FinalizeAsync(state, draft, cancellationToken);
                break;
            }
        }

        state = state with { Draft = draft, UpdatedAt = _clock.GetUtcNow() };
        var saved = await _store.UpdateAsync(state, cancellationToken);
        return ToTurn(saved);
    }

    public async Task<ConversationTurn?> GetAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        var state = await _store.GetAsync(conversationId, cancellationToken);
        return state is null ? null : ToTurn(state);
    }

    private async Task<ConversationState> FinalizeAsync(
        ConversationState state,
        ReportIntakeDraft draft,
        CancellationToken cancellationToken)
    {
        var intake = new ReportIntake(
            draft.RequesterEmail!,
            draft.Purpose!,
            draft.ProjectCode,
            draft.ManagerEmail!,
            draft.Columns ?? [],
            draft.DeliveryFormat!);

        var approvalRequired = state.Kind == NlpRouteKind.GovernancePaused;

        if (approvalRequired && state.AccessRequestId is not null)
        {
            var existing = await _accessRequests.GetAsync(state.AccessRequestId, cancellationToken);
            if (existing is not null)
            {
                await _accessRequests.UpdateAsync(
                    existing with { Intake = intake, Justification = intake.Purpose },
                    cancellationToken);
            }

            await _notifications.SendAsync(
                new NotificationMessage(
                    "manager_notification",
                    intake.ManagerEmail,
                    $"Sensitive data request from {intake.RequesterEmail}",
                    $"{intake.RequesterEmail} has requested sensitive data ({string.Join(", ", state.SensitiveFields)}) and it is awaiting approval."),
                cancellationToken);

            _events.Publish(new AgentEvent("governance.request_enriched", "enriched", state.AccessRequestId));
            _events.Publish(new AgentEvent("governance.manager_notified", "notified", intake.ManagerEmail));
        }
        else if (intake.DeliveryFormat == ReportIntake.Email)
        {
            await _notifications.SendAsync(
                new NotificationMessage(
                    "report_email",
                    intake.RequesterEmail,
                    "Your report is ready",
                    $"Report with columns {string.Join(", ", intake.Columns)}."),
                cancellationToken);

            _events.Publish(new AgentEvent("report.email_simulated", "sent", intake.RequesterEmail));
        }

        if (intake.DeliveryFormat == ReportIntake.Csv && !approvalRequired)
        {
            _events.Publish(new AgentEvent("report.ready", "ready", state.Id));
        }

        _events.Publish(new AgentEvent("conversation.completed", "completed", state.Id));

        return state with
        {
            Step = ConversationStep.Complete,
            ApprovalRequired = approvalRequired,
            UpdatedAt = _clock.GetUtcNow()
        };
    }

    private ConversationTurn ToTurn(ConversationState state)
    {
        var control = state.Step switch
        {
            ConversationStep.Email => ConversationControl.Email,
            ConversationStep.Purpose => ConversationControl.Purpose,
            ConversationStep.Columns => ConversationControl.Columns,
            ConversationStep.Delivery => ConversationControl.Delivery,
            _ => ConversationControl.None
        };

        var columns = state.Step == ConversationStep.Columns
            ? _columns.Available(state.Mql)
            : null;

        var downloadable = state.Step == ConversationStep.Complete
            && !state.ApprovalRequired
            && state.Draft.DeliveryFormat == ReportIntake.Csv;

        return new ConversationTurn(
            state.Id,
            state.Step.ToString(),
            state.Kind.ToString(),
            state.Message ?? StepMessage(state),
            control.ToString().ToLowerInvariant(),
            state.Step == ConversationStep.Email ? state.Draft.RequesterEmail : null,
            columns,
            [ReportIntake.Email, ReportIntake.Csv],
            state.AccessRequestId,
            state.ApprovalRequired,
            downloadable,
            state.DemoReport,
            state.Result,
            null);
    }

    private string StepMessage(ConversationState state)
    {
        return state.Step switch
        {
            ConversationStep.Email => "I can build that report. Which email should receive it?",
            ConversationStep.Purpose => "What is the purpose of this report?",
            ConversationStep.ManagerEmail => "Add your manager's email so they can be notified.",
            ConversationStep.Columns => "Confirm the columns for your report.",
            ConversationStep.Delivery => "Email the report, or download it as CSV?",
            ConversationStep.Complete => CompletionMessage(state),
            _ => state.Utterance
        };
    }

    private string CompletionMessage(ConversationState state)
    {
        if (state.ApprovalRequired)
        {
            return $"Approval is required because this report uses sensitive fields ({string.Join(", ", state.SensitiveFields)}). "
                + $"Your manager {state.Draft.ManagerEmail} has been notified, and request {state.AccessRequestId} is pending lead approval.";
        }

        if (state.Draft.DeliveryFormat == ReportIntake.Csv)
        {
            return "Your report is ready. Use the Download CSV button to save it.";
        }

        return $"Your report has been emailed to {state.Draft.RequesterEmail}.";
    }

    private static string DirectMessage(NlpRouteResult result)
    {
        return result.Kind switch
        {
            NlpRouteKind.CacheHit => "Here is the cached query for that request.",
            NlpRouteKind.SimpleMql => "Here is the query for that request.",
            NlpRouteKind.ClarifyRequired => result.Question ?? "Could you clarify your request?",
            NlpRouteKind.Rejected => $"This request was blocked: {result.GuardrailReason}",
            NlpRouteKind.ComplexLlmFailed => $"The language model is unavailable. {result.Error}",
            _ => "Here is the result for that request."
        };
    }

    private static string? FirstNonEmpty(string? first, string? second)
    {
        return !string.IsNullOrWhiteSpace(first) ? first : !string.IsNullOrWhiteSpace(second) ? second : null;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^[A-Za-z0-9-]+$")]
    private static partial Regex ProjectCodePattern();
}
```

Note: the draft is only persisted after `FinalizeAsync`, so add `state = state with { Draft = draft }` inside the `Delivery` branch before finalizing. Adjust the `Delivery` case to:

```csharp
            case ConversationStep.Delivery:
            {
                var delivery = answer.Delivery?.Trim().ToUpperInvariant();
                if (delivery is not (ReportIntake.Email or ReportIntake.Csv))
                {
                    return ToTurn(state) with { ValidationError = "Choose email delivery or CSV download." };
                }

                draft = draft with { DeliveryFormat = delivery };
                state = await FinalizeAsync(state with { Draft = draft }, draft, cancellationToken);
                break;
            }
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ConversationOrchestratorTests"`
Expected: PASS, 3 tests.

- [ ] **Step 5: Commit**

```bash
git add src/gateway/Conversations tests/Gateway.Tests/Conversations/ConversationOrchestratorTests.cs
git commit -m "feat(conversations): add the report intake step machine

Co-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>"
```

---

### Task 9: Sensitive completion, flag off, and demo fallback

**Files:**
- Modify: `src/gateway/Conversations/ConversationOrchestrator.cs` only if a defect is found.
- Test: `tests/Gateway.Tests/Conversations/ConversationOrchestratorApprovalTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 1 to 8.
- Produces: no new public types; behaviour is verified.

- [ ] **Step 1: Write the failing tests**

```csharp
using Gateway.Conversations;
using Gateway.Governance;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Orchestrator;
using Gateway.Nlp.Router;
using Gateway.Nlp.Http;
using Gateway.Reports;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Conversations;

public class ConversationOrchestratorApprovalTests
{
    private sealed class StubOrchestrator : INlpOrchestrator
    {
        private readonly NlpRouteResult _result;

        public StubOrchestrator(NlpRouteResult result) => _result = result;

        public Task<NlpRouteResult> OrchestrateAsync(string utterance, string sessionId = "anonymous", CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }

    private static NlpRouteResult Paused(string mql) => new(
        NlpRouteKind.GovernancePaused,
        mql,
        null,
        false,
        false,
        Gateway.Nlp.Intent.IntentKind.Search,
        false,
        new Gateway.Nlp.Mql.MqlDefaults(),
        0,
        1,
        null,
        ["address.location.coordinates"],
        "req_1",
        "sensitive field requires approval: address.location.coordinates");

    private static async Task<(ConversationTurn Turn, SimulatedNotificationSender Sender, IAccessRequestStore Requests)> RunAsync(NlpRouteResult result)
    {
        var sender = new SimulatedNotificationSender();
        var requests = new InMemoryAccessRequestStore();
        await requests.CreateAsync(new AccessRequest(
            "req_1",
            "sess_1",
            result.Mql!,
            ["address.location.coordinates"],
            AccessRequest.PendingLead,
            null,
            DateTimeOffset.UnixEpoch));

        var sut = new ConversationOrchestrator(
            new StubOrchestrator(result),
            new InMemoryConversationStore(),
            requests,
            new InMemoryApprovalFlagStore(true),
            sender,
            new ColumnCatalog(),
            new InMemoryAgentEventSink(),
            Options.Create(new ReportsOptions()),
            TimeProvider.System);

        var turn = await sut.StartAsync("average coordinates near me", "sess_1", "analyst@enterprise.com");
        turn = await sut.AnswerAsync(turn.ConversationId, new ConversationAnswer(Email: "analyst@enterprise.com"));
        turn = await sut.AnswerAsync(turn.ConversationId, new ConversationAnswer(Purpose: "Geo analysis"));
        turn = await sut.AnswerAsync(turn.ConversationId, new ConversationAnswer(ManagerEmail: "manager@enterprise.com"));
        turn = await sut.AnswerAsync(turn.ConversationId, new ConversationAnswer(Columns: ["name"]));
        turn = await sut.AnswerAsync(turn.ConversationId, new ConversationAnswer(Delivery: ReportIntake.Csv));
        return (turn!, sender, requests);
    }

    [Fact]
    public async Task Sensitive_completion_requires_approval_and_notifies_the_manager()
    {
        var (turn, sender, requests) = await RunAsync(Paused("""[{"$match":{"address.location.coordinates.lat":{"$gte":40}}}]"""));

        Assert.True(turn.ApprovalRequired);
        Assert.False(turn.Downloadable);
        Assert.Contains("PENDING_LEAD", turn.AssistantMessage);
        Assert.Single(sender.Sent);
        Assert.Equal("manager_notification", sender.Sent[0].Kind);
        Assert.Equal("manager@enterprise.com", sender.Sent[0].Recipient);

        var stored = await requests.GetAsync("req_1");
        Assert.Equal("manager@enterprise.com", stored!.Intake!.ManagerEmail);
    }

    [Fact]
    public async Task Email_delivery_is_simulated_for_non_sensitive_reports()
    {
        var result = Paused("""[{"$match":{"address.location.coordinates.lat":{"$gte":40}}}]""") with
        {
            Kind = NlpRouteKind.ComplexLlmRequired,
            SensitiveFields = []
        };

        var (turn, sender, _) = await RunAsync(result);

        Assert.False(turn.ApprovalRequired);
        Assert.Single(sender.Sent);
        Assert.Equal("report_email", sender.Sent[0].Kind);
    }
}
```

- [ ] **Step 2: Run the tests to verify they pass or expose defects**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ConversationOrchestratorApprovalTests"`
Expected: PASS if Task 8's finalize logic is correct. If `turn.AssistantMessage` does not contain `PENDING_LEAD`, add that word to `CompletionMessage` in the same message.

- [ ] **Step 3: Fix any defect in `ConversationOrchestrator`**

If the access request is not enriched, confirm that `FinalizeAsync` receives `state with { Draft = draft }` and that the `AccessRequest` lookup uses `state.AccessRequestId`.

- [ ] **Step 4: Run the full conversation suite**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~Conversation"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/gateway/Conversations tests/Gateway.Tests/Conversations
git commit -m "test(conversations): cover approval and simulated delivery

Co-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>"
```

---

### Task 10: Wire the new services into DI

**Files:**
- Modify: `src/gateway/Nlp/NlpServiceCollectionExtensions.cs`
- Test: `tests/Gateway.Tests/Conversations/ConversationDiTests.cs`

**Interfaces:**
- Consumes: all new types.
- Produces: `AddGatewayNlp` registers `IApprovalFlagStore`, `IConversationStore`, `INotificationSender`, `IColumnCatalog`, and `ConversationOrchestrator`.

- [ ] **Step 1: Write the failing test**

```csharp
using Gateway.Conversations;
using Gateway.Governance;
using Gateway.Nlp;
using Gateway.Reports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Conversations;

public class ConversationDiTests
{
    [Fact]
    public void AddGatewayNlp_registers_the_conversation_services()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new StubEnvironment());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        services.AddGatewayNlp(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IApprovalFlagStore>());
        Assert.NotNull(provider.GetService<IConversationStore>());
        Assert.NotNull(provider.GetService<INotificationSender>());
        Assert.NotNull(provider.GetService<IColumnCatalog>());
        Assert.NotNull(provider.GetService<ConversationOrchestrator>());
    }

    private sealed class StubEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Gateway.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ConversationDiTests"`
Expected: FAIL because the services are not registered.

- [ ] **Step 3: Update the DI registration**

In `src/gateway/Nlp/NlpServiceCollectionExtensions.cs`, add the usings:
```csharp
using Gateway.Conversations;
using Gateway.Governance;
using Gateway.Reports;
```
Add the options bindings next to the existing ones:
```csharp
        services.Configure<GovernanceOptions>(configuration.GetSection(GovernanceOptions.SectionName));
        services.Configure<ReportsOptions>(configuration.GetSection(ReportsOptions.SectionName));
```
Add the registrations next to the guardrail registrations:
```csharp
        services.AddSingleton<IApprovalFlagStore>(sp => new InMemoryApprovalFlagStore(
            sp.GetRequiredService<IOptions<GovernanceOptions>>().Value.ApprovalEnabled));
        services.AddSingleton<IConversationStore, InMemoryConversationStore>();
        services.AddSingleton<INotificationSender, SimulatedNotificationSender>();
        services.AddSingleton<IColumnCatalog, ColumnCatalog>();
        services.AddSingleton<ConversationOrchestrator>();
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ConversationDiTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/gateway/Nlp/NlpServiceCollectionExtensions.cs tests/Gateway.Tests/Conversations/ConversationDiTests.cs
git commit -m "feat(conversations): wire report intake services into di

Co-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>"
```

---

### Task 11: Conversation and governance endpoints

**Files:**
- Modify: `src/gateway/Program.cs`
- Test: `tests/Gateway.Tests/ApiConversationTests.cs`

**Interfaces:**
- Consumes: `ConversationOrchestrator`, `IApprovalFlagStore`, `ReportService`, `SessionClaims`.
- Produces: `POST /api/conversations`, `POST /api/conversations/{id}/answers`, `GET /api/conversations/{id}`, `GET /api/conversations/{id}/report.csv`, `GET /api/governance/approval`, `PUT /api/governance/approval`.

- [ ] **Step 1: Write the failing test**

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Gateway.Tests;

public class ApiConversationTests : IClassFixture<GatewayFactory>
{
    private readonly GatewayFactory _factory;

    public ApiConversationTests(GatewayFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Conversation_start_without_token_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/conversations", new { utterance = "average price by market" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Complex_query_starts_the_intake_and_a_direct_query_completes()
    {
        var client = AuthedClient("Data Owner / Admin");

        var report = await client.PostAsJsonAsync("/api/conversations", new { utterance = "average price by market" });
        var reportTurn = await report.Content.ReadFromJsonAsync<ConversationTurnDto>();
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        Assert.Equal("Email", reportTurn!.Step);

        var direct = await client.PostAsJsonAsync("/api/conversations", new { utterance = "listings in Los Angeles" });
        var directTurn = await direct.Content.ReadFromJsonAsync<ConversationTurnDto>();
        Assert.Equal("Complete", directTurn!.Step);
    }

    [Fact]
    public async Task Approval_flag_blocks_non_privileged_roles()
    {
        var privileged = AuthedClient("Data Owner / Admin");
        var denied = AuthedClient("Business Analyst");

        var allowed = await privileged.PutAsJsonAsync("/api/governance/approval", new { enabled = false });
        var forbidden = await denied.PutAsJsonAsync("/api/governance/approval", new { enabled = true });

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        await privileged.PutAsJsonAsync("/api/governance/approval", new { enabled = true });
    }

    [Fact]
    public async Task Csv_endpoint_blocks_while_awaiting_approval()
    {
        var client = AuthedClient("Business Analyst");

        var start = await client.PostAsJsonAsync("/api/conversations", new { utterance = "average coordinates near me" });
        var turn = await start.Content.ReadFromJsonAsync<ConversationTurnDto>();
        if (turn!.Step != "Email")
        {
            return;
        }

        var answers = new object[]
        {
            new { email = "analyst@enterprise.com" },
            new { purpose = "Geo analysis", projectCode = "PROJ-GEO" },
            new { managerEmail = "manager@enterprise.com" },
            new { columns = new[] { "name" } },
            new { delivery = "CSV" }
        };

        foreach (var answer in answers)
        {
            var next = await client.PostAsJsonAsync($"/api/conversations/{turn.ConversationId}/answers", answer);
            turn = await next.Content.ReadFromJsonAsync<ConversationTurnDto>();
        }

        var csv = await client.GetAsync($"/api/conversations/{turn!.ConversationId}/report.csv");

        Assert.True(csv.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.OK);
    }

    private HttpClient AuthedClient(string role)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", IssueToken(role));
        return client;
    }

    private static string IssueToken(string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GatewayFactory.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim("user_id", "usr_test"),
            new Claim("name", "Test"),
            new Claim("email", "analyst@enterprise.com"),
            new Claim("role", role),
            new Claim("lead_user_id", "usr_lead")
        };
        var token = new JwtSecurityToken(
            issuer: GatewayFactory.Issuer,
            audience: GatewayFactory.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record ConversationTurnDto(
        string ConversationId,
        string Step,
        string Kind,
        string AssistantMessage,
        string Control,
        bool ApprovalRequired,
        bool Downloadable);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ApiConversationTests"`
Expected: FAIL with 404 for the new routes.

- [ ] **Step 3: Add the endpoints**

In `src/gateway/Program.cs`, add the usings:
```csharp
using Gateway.Conversations;
using Gateway.Governance;
using Gateway.Reports;
```
Add the endpoints after the `/api/nlp/query` mapping:
```csharp
app.MapPost("/api/conversations", async (
    ConversationStartRequest request,
    ClaimsPrincipal user,
    ConversationOrchestrator conversations,
    CancellationToken cancellationToken) =>
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
    var email = user.FindFirst(SessionClaims.Email)?.Value;
    var turn = await conversations.StartAsync(utterance, sessionId, email, cancellationToken);
    return Results.Ok(turn);
}).RequireAuthorization();

app.MapPost("/api/conversations/{id}/answers", async (
    string id,
    ConversationAnswer answer,
    ConversationOrchestrator conversations,
    CancellationToken cancellationToken) =>
{
    var turn = await conversations.AnswerAsync(id, answer, cancellationToken);
    return turn is null ? Results.NotFound() : Results.Ok(turn);
}).RequireAuthorization();

app.MapGet("/api/conversations/{id}", async (
    string id,
    ConversationOrchestrator conversations,
    CancellationToken cancellationToken) =>
{
    var turn = await conversations.GetAsync(id, cancellationToken);
    return turn is null ? Results.NotFound() : Results.Ok(turn);
}).RequireAuthorization();

app.MapGet("/api/conversations/{id}/report.csv", async (
    string id,
    ConversationOrchestrator conversations,
    CancellationToken cancellationToken) =>
{
    var turn = await conversations.GetAsync(id, cancellationToken);
    if (turn is null)
    {
        return Results.NotFound();
    }

    if (turn.Step != "Complete")
    {
        return Results.Conflict(new { error = "conversation is not complete" });
    }

    var columns = turn.Columns?.Where(column => column.Selected).Select(column => column.Name).ToList() ?? [];
    var build = ReportService.BuildCsv(columns, turn.ApprovalRequired);
    if (!build.Ready)
    {
        return Results.Conflict(new { error = build.Reason });
    }

    return Results.Text(build.Csv, "text/csv");
}).RequireAuthorization();

app.MapGet("/api/governance/approval", (IApprovalFlagStore flags) =>
    Results.Ok(new { enabled = flags.Enabled })).RequireAuthorization();

app.MapPut("/api/governance/approval", async (
    ApprovalFlagRequest request,
    ClaimsPrincipal user,
    IApprovalFlagStore flags,
    CancellationToken cancellationToken) =>
{
    var role = user.FindFirst(SessionClaims.Role)?.Value;
    if (role != "Data Owner / Admin")
    {
        return Results.Forbid();
    }

    await flags.SetAsync(request.Enabled, cancellationToken);
    return Results.Ok(new { enabled = flags.Enabled });
}).RequireAuthorization();
```
Add the request record at the bottom of `Program.cs` before `public partial class Program;`:
```csharp
public sealed record ApprovalFlagRequest(bool Enabled);
```

Important: `turn.Columns` is only populated while the step is `Columns`. For a completed conversation the CSV endpoint needs the confirmed columns. Fix `ToTurn` in `ConversationOrchestrator` so columns are returned whenever `state.Draft.Columns is not null`:
```csharp
        var columns = state.Step == ConversationStep.Columns
            ? _columns.Available(state.Mql)
            : state.Draft.Columns?.Select(name => new ColumnOption(name, true)).ToList();
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj --filter "FullyQualifiedName~ApiConversationTests"`
Expected: PASS, 4 tests.

- [ ] **Step 5: Commit**

```bash
git add src/gateway/Program.cs src/gateway/Conversations tests/Gateway.Tests/ApiConversationTests.cs
git commit -m "feat(api): add conversation, report, and approval endpoints

Co-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>"
```

---

### Task 12: SPA conversation models and service

**Files:**
- Create: `src/web/src/app/models/conversation.ts`
- Create: `src/web/src/app/services/conversation.service.ts`
- Test: `src/web/src/app/services/conversation.service.spec.ts`

**Interfaces:**
- Produces: `ConversationTurn`, `ColumnOption`, `ConversationAnswer`, `ConversationService.start`, `answer`, `get`, `downloadReport`, `getApproval`, `setApproval`.

- [ ] **Step 1: Write the failing test**

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ConversationService } from './conversation.service';

describe('ConversationService', () => {
  let service: ConversationService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(ConversationService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('starts a conversation', () => {
    service.start('average price by market').subscribe((turn) => {
      expect(turn.step).toBe('Email');
      expect(turn.control).toBe('email');
    });

    const req = http.expectOne('/api/conversations');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ utterance: 'average price by market' });
    req.flush({ conversationId: 'c1', step: 'Email', kind: 'ComplexLlmRequired', assistantMessage: 'Email?', control: 'email', deliveryOptions: ['EMAIL', 'CSV'] });
  });

  it('answers and reads the approval flag', () => {
    service.answer('c1', { delivery: 'CSV' }).subscribe();
    http.expectOne('/api/conversations/c1/answers').flush({ conversationId: 'c1', step: 'Complete' });

    service.setApproval(false).subscribe((state) => expect(state.enabled).toBe(false));
    const put = http.expectOne('/api/governance/approval');
    expect(put.request.method).toBe('PUT');
    put.flush({ enabled: false });
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npm test` in `src/web`
Expected: FAIL because `conversation.service.ts` does not exist.

- [ ] **Step 3: Write the implementation**

`src/web/src/app/models/conversation.ts`
```typescript
export interface ColumnOption {
  name: string;
  selected: boolean;
}

export interface ConversationTurn {
  conversationId: string;
  step: string;
  kind: string;
  assistantMessage: string;
  control: string;
  emailPrefill?: string | null;
  columns?: ColumnOption[] | null;
  deliveryOptions: string[];
  accessRequestId?: string | null;
  approvalRequired: boolean;
  downloadable: boolean;
  demoReport: boolean;
  result?: unknown;
  validationError?: string | null;
}

export interface ConversationAnswer {
  text?: string;
  email?: string;
  purpose?: string;
  projectCode?: string;
  managerEmail?: string;
  columns?: string[];
  delivery?: string;
}

export interface ApprovalFlag {
  enabled: boolean;
}
```

`src/web/src/app/services/conversation.service.ts`
```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApprovalFlag, ConversationAnswer, ConversationTurn } from '../models/conversation';

@Injectable({ providedIn: 'root' })
export class ConversationService {
  constructor(private readonly http: HttpClient) {}

  start(utterance: string): Observable<ConversationTurn> {
    return this.http.post<ConversationTurn>('/api/conversations', { utterance });
  }

  answer(conversationId: string, answer: ConversationAnswer): Observable<ConversationTurn> {
    return this.http.post<ConversationTurn>(`/api/conversations/${conversationId}/answers`, answer);
  }

  get(conversationId: string): Observable<ConversationTurn> {
    return this.http.get<ConversationTurn>(`/api/conversations/${conversationId}`);
  }

  downloadReport(conversationId: string): Observable<Blob> {
    return this.http.get(`/api/conversations/${conversationId}/report.csv`, { responseType: 'blob' });
  }

  getApproval(): Observable<ApprovalFlag> {
    return this.http.get<ApprovalFlag>('/api/governance/approval');
  }

  setApproval(enabled: boolean): Observable<ApprovalFlag> {
    return this.http.put<ApprovalFlag>('/api/governance/approval', { enabled });
  }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `npm test` in `src/web`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/web/src/app/models/conversation.ts src/web/src/app/services/conversation.service.ts src/web/src/app/services/conversation.service.spec.ts
git commit -m "feat(web): add conversation models and service

Co-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>"
```

---

### Task 13: Chat thread component

**Files:**
- Create: `src/web/src/app/chat/chat-thread.component.ts`
- Create: `src/web/src/app/chat/chat-thread.component.html`
- Create: `src/web/src/app/chat/chat-thread.component.scss`
- Test: `src/web/src/app/chat/chat-thread.component.spec.ts`

**Interfaces:**
- Consumes: `ConversationService`, `ConversationTurn`, `Greeting`.
- Produces: `<app-chat-thread [greeting]="greeting"></app-chat-thread>`.

- [ ] **Step 1: Write the failing test**

```typescript
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ChatThreadComponent } from './chat-thread.component';
import { ConversationService } from '../services/conversation.service';

const emailTurn = {
  conversationId: 'c1',
  step: 'Email',
  kind: 'ComplexLlmRequired',
  assistantMessage: 'Which email should receive it?',
  control: 'email',
  deliveryOptions: ['EMAIL', 'CSV'],
  approvalRequired: false,
  downloadable: false,
  demoReport: false
};

const columnsTurn = {
  conversationId: 'c1',
  step: 'Columns',
  kind: 'ComplexLlmRequired',
  assistantMessage: 'Confirm the columns.',
  control: 'columns',
  columns: [
    { name: 'name', selected: true },
    { name: 'price', selected: true }
  ],
  deliveryOptions: ['EMAIL', 'CSV'],
  approvalRequired: false,
  downloadable: false,
  demoReport: false
};

describe('ChatThreadComponent', () => {
  let startCalls: string[];
  let answerCalls: unknown[];

  beforeEach(async () => {
    startCalls = [];
    answerCalls = [];
    await TestBed.configureTestingModule({
      imports: [ChatThreadComponent],
      providers: [
        {
          provide: ConversationService,
          useValue: {
            start: (utterance: string) => {
              startCalls.push(utterance);
              return of(emailTurn);
            },
            answer: (_id: string, answer: unknown) => {
              answerCalls.push(answer);
              return of(columnsTurn);
            },
            downloadReport: () => of(new Blob()),
            get: () => of(emailTurn),
            getApproval: () => of({ enabled: true }),
            setApproval: () => of({ enabled: true })
          }
        }
      ]
    }).compileComponents();
  });

  it('uses a Send button with an accessible label', () => {
    const fixture = TestBed.createComponent(ChatThreadComponent);
    fixture.detectChanges();
    const button = fixture.nativeElement.querySelector('button[type="submit"]') as HTMLButtonElement;

    expect(button.getAttribute('aria-label')).toBe('Send message');
    expect(button.textContent).toContain('Send');
  });

  it('starts a conversation and renders the email control', () => {
    const fixture = TestBed.createComponent(ChatThreadComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    component.text = 'average price by market';
    component.send();
    fixture.detectChanges();

    expect(startCalls).toEqual(['average price by market']);
    expect(fixture.nativeElement.querySelector('#step-email')).toBeTruthy();
  });

  it('submits selected columns', () => {
    const fixture = TestBed.createComponent(ChatThreadComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    component.text = 'average price by market';
    component.send();
    fixture.detectChanges();
    component.submitEmail();
    fixture.detectChanges();

    component.selectedColumns = new Set(['name']);
    component.submitColumns();

    expect(answerCalls[0]).toEqual({ email: component.email });
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npm test` in `src/web`
Expected: FAIL because `chat-thread.component.ts` does not exist.

- [ ] **Step 3: Write the implementation**

`src/web/src/app/chat/chat-thread.component.ts`
```typescript
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Greeting } from '../models/greeting';
import { ConversationAnswer, ConversationTurn } from '../models/conversation';
import { ConversationService } from '../services/conversation.service';

interface ChatMessage {
  role: 'user' | 'assistant';
  text: string;
  demo?: boolean;
}

@Component({
  selector: 'app-chat-thread',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './chat-thread.component.html',
  styleUrl: './chat-thread.component.scss'
})
export class ChatThreadComponent {
  @Input() greeting: Greeting | null = null;

  messages: ChatMessage[] = [];
  turn: ConversationTurn | null = null;
  text = '';
  email = '';
  purpose = '';
  projectCode = '';
  managerEmail = '';
  selectedColumns = new Set<string>();
  delivery = 'CSV';
  pending = false;
  error: string | null = null;

  constructor(private readonly conversations: ConversationService) {}

  send(): void {
    const utterance = this.text.trim();
    if (!utterance) {
      return;
    }

    this.messages = [...this.messages, { role: 'user', text: utterance }];
    this.pending = true;
    this.error = null;

    this.conversations.start(utterance).subscribe({
      next: (turn) => this.apply(turn),
      error: () => {
        this.error = 'Query failed. Is the gateway running?';
        this.pending = false;
      }
    });
  }

  submitEmail(): void {
    this.answer({ email: this.email.trim() });
  }

  submitPurpose(): void {
    this.answer({ purpose: this.purpose.trim(), projectCode: this.projectCode.trim() || undefined });
  }

  submitManagerEmail(): void {
    this.answer({ managerEmail: this.managerEmail.trim() });
  }

  submitColumns(): void {
    this.answer({ columns: Array.from(this.selectedColumns) });
  }

  submitDelivery(): void {
    this.answer({ delivery: this.delivery });
  }

  toggleColumn(name: string, checked: boolean): void {
    const next = new Set(this.selectedColumns);
    if (checked) {
      next.add(name);
    } else {
      next.delete(name);
    }
    this.selectedColumns = next;
  }

  useChip(chip: string): void {
    this.text = chip;
    this.send();
  }

  download(): void {
    if (!this.turn?.downloadable) {
      return;
    }

    this.conversations.downloadReport(this.turn.conversationId).subscribe((blob) => {
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = 'report.csv';
      anchor.click();
      URL.revokeObjectURL(url);
    });
  }

  private answer(answer: ConversationAnswer): void {
    if (!this.turn) {
      return;
    }

    this.pending = true;
    this.error = null;
    this.conversations.answer(this.turn.conversationId, answer).subscribe({
      next: (turn) => {
        if (answer.email) {
          this.messages = [...this.messages, { role: 'user', text: answer.email }];
        } else if (answer.purpose) {
          this.messages = [...this.messages, { role: 'user', text: answer.purpose }];
        } else if (answer.managerEmail) {
          this.messages = [...this.messages, { role: 'user', text: answer.managerEmail }];
        }
        this.apply(turn);
      },
      error: () => {
        this.error = 'Unable to continue the conversation.';
        this.pending = false;
      }
    });
  }

  private apply(turn: ConversationTurn): void {
    this.turn = turn;
    this.pending = false;
    this.messages = [...this.messages, { role: 'assistant', text: turn.assistantMessage, demo: turn.demoReport }];
    if (turn.control === 'email') {
      this.email = turn.emailPrefill ?? this.email;
    }
    if (turn.control === 'columns' && turn.columns) {
      this.selectedColumns = new Set(turn.columns.filter((column) => column.selected).map((column) => column.name));
    }
    if (turn.validationError) {
      this.error = turn.validationError;
    }
  }
}
```

`src/web/src/app/chat/chat-thread.component.html`
```html
<section class="chat">
  <div class="thread" role="log" aria-live="polite">
    @for (message of messages; track $index) {
      <article class="bubble" [class.user]="message.role === 'user'" [class.assistant]="message.role === 'assistant'">
        <p>{{ message.text }}</p>
        @if (message.demo) {
          <p class="demo">Demo report</p>
        }
      </article>
    }
    @if (pending) {
      <p class="typing">Working…</p>
    }
  </div>

  @if (!turn) {
    @if (greeting) {
      <div class="chips">
        @for (chip of greeting.chips; track chip) {
          <button type="button" class="chip" (click)="useChip(chip)">{{ chip }}</button>
        }
      </div>
    }
  }

  @if (error) {
    <p class="error" role="alert">{{ error }}</p>
  }

  @if (turn?.control === 'email') {
    <form class="step" (ngSubmit)="submitEmail()">
      <label for="step-email">Delivery email</label>
      <input id="step-email" name="email" type="email" [(ngModel)]="email" required />
      <button type="submit" [disabled]="pending">Confirm email</button>
    </form>
  } @else if (turn?.control === 'purpose') {
    <form class="step" (ngSubmit)="submitPurpose()">
      <label for="step-purpose">Purpose</label>
      <textarea id="step-purpose" name="purpose" [(ngModel)]="purpose" required></textarea>
      <label for="step-project">Project code (optional)</label>
      <input id="step-project" name="projectCode" [(ngModel)]="projectCode" />
      <button type="submit" [disabled]="pending">Confirm purpose</button>
    </form>
  } @else if (turn?.control === 'columns') {
    <fieldset class="step">
      <legend>Columns</legend>
      @for (column of turn?.columns ?? []; track column.name) {
        <label class="column">
          <input
            type="checkbox"
            [checked]="selectedColumns.has(column.name)"
            (change)="toggleColumn(column.name, $any($event.target).checked)" />
          {{ column.name }}
        </label>
      }
      <button type="button" [disabled]="pending" (click)="submitColumns()">Confirm columns</button>
    </fieldset>
  } @else if (turn?.control === 'delivery') {
    <fieldset class="step">
      <legend>Delivery</legend>
      <label><input type="radio" name="delivery" value="EMAIL" [(ngModel)]="delivery" /> Email report</label>
      <label><input type="radio" name="delivery" value="CSV" [(ngModel)]="delivery" /> Download CSV</label>
      <button type="button" [disabled]="pending" (click)="submitDelivery()">Confirm delivery</button>
    </fieldset>
  } @else if (turn?.control === 'manager') {
    <form class="step" (ngSubmit)="submitManagerEmail()">
      <label for="step-manager">Manager email</label>
      <input id="step-manager" name="managerEmail" type="email" [(ngModel)]="managerEmail" required />
      <button type="submit" [disabled]="pending">Confirm manager</button>
    </form>
  }

  @if (turn?.downloadable) {
    <button type="button" class="download" (click)="download()">Download CSV</button>
  }

  @if (!turn || turn?.step === 'Complete') {
    <form class="composer" (ngSubmit)="send()">
      <label class="sr-only" for="utterance">Ask about listings</label>
      <input id="utterance" name="utterance" [(ngModel)]="text" placeholder="Ask about listings" />
      <button type="submit" aria-label="Send message" [disabled]="pending">Send</button>
    </form>
  }
</section>
```

`src/web/src/app/chat/chat-thread.component.scss`
```scss
.chat {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  height: 100%;
}

.thread {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  overflow-y: auto;
  flex: 1;
}

.bubble {
  max-width: 70%;
  padding: 0.6rem 0.8rem;
  border-radius: 0.75rem;
  background: #f2f4f8;
}

.bubble.user {
  align-self: flex-end;
  background: #1f4fa3;
  color: #fff;
}

.bubble.assistant {
  align-self: flex-start;
}

.demo {
  font-size: 0.75rem;
  color: #8a5a00;
}

.typing,
.error {
  font-size: 0.85rem;
}

.error {
  color: #b00020;
}

.chips,
.step,
.composer {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  align-items: center;
}

.step {
  flex-direction: column;
  align-items: stretch;
}

.column {
  display: flex;
  gap: 0.4rem;
  align-items: center;
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0 0 0 0);
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `npm test` in `src/web`
Expected: PASS. The manager control string is `manageremail` from the enum `ManagerEmail` lowercased; in the template use `turn?.control === 'manageremail'`. Correct the template branch accordingly.

- [ ] **Step 5: Commit**

```bash
git add src/web/src/app/chat
git commit -m "feat(web): add chat thread with inline intake controls

Co-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>"
```

---

### Task 14: Governance toggle component

**Files:**
- Create: `src/web/src/app/governance/governance-toggle.component.ts`
- Test: `src/web/src/app/governance/governance-toggle.component.spec.ts`

**Interfaces:**
- Consumes: `ConversationService.getApproval`, `setApproval`.
- Produces: `<app-governance-toggle [visible]="canManageGovernance"></app-governance-toggle>`.

- [ ] **Step 1: Write the failing test**

```typescript
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { GovernanceToggleComponent } from './governance-toggle.component';
import { ConversationService } from '../services/conversation.service';

describe('GovernanceToggleComponent', () => {
  let saved: boolean[];

  beforeEach(async () => {
    saved = [];
    await TestBed.configureTestingModule({
      imports: [GovernanceToggleComponent],
      providers: [
        {
          provide: ConversationService,
          useValue: {
            getApproval: () => of({ enabled: true }),
            setApproval: (enabled: boolean) => {
              saved.push(enabled);
              return of({ enabled });
            }
          }
        }
      ]
    }).compileComponents();
  });

  it('is hidden when not visible', () => {
    const fixture = TestBed.createComponent(GovernanceToggleComponent);
    fixture.componentInstance.visible = false;
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('#governance-toggle')).toBeNull();
  });

  it('loads and saves the flag', () => {
    const fixture = TestBed.createComponent(GovernanceToggleComponent);
    fixture.componentInstance.visible = true;
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('#governance-toggle') as HTMLInputElement;
    expect(input.checked).toBe(true);

    fixture.componentInstance.toggle(false);

    expect(saved).toEqual([false]);
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npm test` in `src/web`
Expected: FAIL because the component does not exist.

- [ ] **Step 3: Write the implementation**

`src/web/src/app/governance/governance-toggle.component.ts`
```typescript
import { Component, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ConversationService } from '../services/conversation.service';

@Component({
  selector: 'app-governance-toggle',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (visible) {
      <label class="governance">
        <input
          id="governance-toggle"
          type="checkbox"
          [checked]="enabled"
          (change)="toggle($any($event.target).checked)" />
        Approval gate
      </label>
      @if (error) {
        <span class="error" role="alert">{{ error }}</span>
      }
    }
  `,
  styles: [`
    .governance {
      display: inline-flex;
      gap: 0.4rem;
      align-items: center;
      font-size: 0.85rem;
    }
    .error {
      color: #b00020;
      font-size: 0.8rem;
    }
  `]
})
export class GovernanceToggleComponent implements OnInit {
  @Input() visible = false;

  enabled = true;
  error: string | null = null;

  constructor(private readonly conversations: ConversationService) {}

  ngOnInit(): void {
    if (!this.visible) {
      return;
    }

    this.conversations.getApproval().subscribe({
      next: (flag) => (this.enabled = flag.enabled),
      error: () => (this.error = 'Unable to read the approval flag.')
    });
  }

  toggle(enabled: boolean): void {
    this.enabled = enabled;
    this.conversations.setApproval(enabled).subscribe({
      next: (flag) => {
        this.enabled = flag.enabled;
        this.error = null;
      },
      error: () => {
        this.enabled = !enabled;
        this.error = 'Unable to change the approval flag.';
      }
    });
  }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `npm test` in `src/web`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/web/src/app/governance
git commit -m "feat(web): add role-restricted governance toggle

Co-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>"
```

---

### Task 15: Session role, app shell, and stream events

**Files:**
- Modify: `src/web/src/app/services/session.service.ts`
- Modify: `src/web/src/app/services/session.service.spec.ts`
- Modify: `src/web/src/app/services/agent-stream.service.ts`
- Modify: `src/web/src/app/app.component.ts`
- Modify: `src/web/src/app/app.component.html`
- Modify: `src/web/src/app/app.component.spec.ts`

**Interfaces:**
- Consumes: `ChatThreadComponent`, `GovernanceToggleComponent`.
- Produces: `SessionService.role()` returning the JWT `role` claim or null.

- [ ] **Step 1: Write the failing test**

Add to `src/web/src/app/services/session.service.spec.ts`:
```typescript
  it('reads the role from the stored token', () => {
    const payload = btoa(JSON.stringify({ role: 'Data Owner / Admin' }));
    sessionStorage.setItem('access_token', `header.${payload}.signature`);

    expect(service.role()).toBe('Data Owner / Admin');
  });
```
Update the existing `h1` assertion in `src/web/src/app/app.component.spec.ts` to look for `app-chat-thread` and provide `ConversationService` instead of `NlpQueryService`:
```typescript
        {
          provide: ConversationService,
          useValue: {
            start: () => of({ conversationId: 'c1', step: 'Email', kind: 'ComplexLlmRequired', assistantMessage: 'Email?', control: 'email', deliveryOptions: ['EMAIL', 'CSV'], approvalRequired: false, downloadable: false, demoReport: false }),
            answer: () => of({}),
            get: () => of({}),
            downloadReport: () => of(new Blob()),
            getApproval: () => of({ enabled: true }),
            setApproval: () => of({ enabled: true })
          }
        }
```
and the assertion:
```typescript
    expect(compiled.querySelector('app-chat-thread')).toBeTruthy();
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `npm test` in `src/web`
Expected: FAIL because `role()` and the chat host do not exist.

- [ ] **Step 3: Write the implementation**

Add to `src/web/src/app/services/session.service.ts`:
```typescript
  role(): string | null {
    const token = this.token();
    if (!token) {
      return null;
    }

    const payload = token.split('.')[1];
    if (!payload) {
      return null;
    }

    try {
      const decoded = JSON.parse(atob(payload));
      return typeof decoded.role === 'string' ? decoded.role : null;
    } catch {
      return null;
    }
  }
```

Extend the `names` array in `src/web/src/app/services/agent-stream.service.ts`:
```typescript
      const names = [
        'agent.idle',
        'agent.started',
        'agent.clarifying',
        'agent.completed',
        'governance.paused',
        'governance.request_enriched',
        'governance.manager_notified',
        'report.ready',
        'report.email_simulated',
        'conversation.started',
        'conversation.completed'
      ];
```

Replace `src/web/src/app/app.component.ts` with:
```typescript
import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { retry, Subscription, switchMap, timer } from 'rxjs';
import { Greeting } from './models/greeting';
import { AgentStreamService } from './services/agent-stream.service';
import { SessionService } from './services/session.service';
import { ChatThreadComponent } from './chat/chat-thread.component';
import { GovernanceToggleComponent } from './governance/governance-toggle.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, ChatThreadComponent, GovernanceToggleComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit, OnDestroy {
  greeting: Greeting | null = null;
  streamStatus = 'connecting';
  agentActivity: string[] = [];
  canManageGovernance = false;
  error: string | null = null;
  private sub = new Subscription();

  constructor(
    private readonly session: SessionService,
    private readonly agents: AgentStreamService
  ) {}

  ngOnInit(): void {
    this.session.sessionId();
    this.canManageGovernance = this.session.role() === 'Data Owner / Admin';
    this.sub.add(
      this.session.bootstrap().pipe(switchMap(() => this.session.greeting())).subscribe({
        next: (greeting) => {
          this.greeting = greeting;
          this.canManageGovernance = this.session.role() === 'Data Owner / Admin';
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

Replace `src/web/src/app/app.component.html` with:
```html
<main class="shell">
  <header class="hero">
    <p class="eyebrow">Enterprise Multi-Agent Workspace</p>
    @if (greeting) {
      <h1>{{ greeting.message }}</h1>
      <p class="period">Session period: {{ greeting.period }}</p>
      <app-governance-toggle [visible]="canManageGovernance"></app-governance-toggle>
    } @else if (error) {
      <h1>Workspace unavailable</h1>
      <p>{{ error }}</p>
    } @else {
      <h1>Signing you in…</h1>
    }
  </header>

  <div class="layout">
    <app-chat-thread [greeting]="greeting"></app-chat-thread>

    <aside class="telemetry">
      <h2>Agent swarm</h2>
      <p>Transport: Server-Sent Events</p>
      <p class="status">Status: {{ streamStatus }}</p>
      <ul class="activity">
        @for (event of agentActivity; track $index) {
          <li>{{ event }}</li>
        }
      </ul>
    </aside>
  </div>
</main>
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `npm test` in `src/web`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/web/src/app
git commit -m "feat(web): host the chat thread and governance toggle

Co-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>"
```

---

### Task 16: Documentation and full verification

**Files:**
- Modify: `README.md`
- Modify: `src/web/src/app/app.component.scss` only if the layout needs flex sizing for `.layout`.

**Interfaces:**
- Consumes: everything.
- Produces: none.

- [ ] **Step 1: Add the layout rule**

Append to `src/web/src/app/app.component.scss`:
```scss
.layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 260px;
  gap: 1rem;
  align-items: start;
}

@media (max-width: 900px) {
  .layout {
    grid-template-columns: minmax(0, 1fr);
  }
}
```

- [ ] **Step 2: Update the README**

Add after the "API reference (Development)" section:
```markdown
## Report intake chat

Report-producing queries open a chat intake that collects the delivery email, purpose,
manager notification email, report columns, and delivery choice. A sensitive query creates
a `PENDING_LEAD` access request and notifies the manager. A global approval flag
(`Governance:ApprovalEnabled`, default `true`) can be toggled at runtime by a
`Data Owner / Admin` through the header switch or `PUT /api/governance/approval`. CSV
downloads use in-memory demo rows until the MongoDB execution and SMTP export work lands.
```

- [ ] **Step 3: Run the full gateway suite**

Run: `export PATH="$PATH:/root/.dotnet" && dotnet test tests/Gateway.Tests/Gateway.Tests.csproj`
Expected: PASS with the previous 127 tests plus the new tests. Then run:
```bash
git restore docs/benchmarks/
```

- [ ] **Step 4: Run the full web suite**

Run: `npm test` in `src/web`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add README.md src/web/src/app/app.component.scss
git commit -m "docs: document the report intake chat and approval flag

Co-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>"
```

---

## Self-Review

**Spec coverage**

- Section 5 steps: Task 8 (`Email`, `Purpose`, `ManagerEmail`, `Columns`, `Delivery`, `Complete`) with validation.
- Section 6 trigger rules: Task 8 (`StartAsync` branch on route kind) and Task 9 (sensitive and demo).
- Section 7 approval flag: Tasks 1, 2, 10, 11, 14.
- Section 8 delivery and CSV: Tasks 5, 6, 11.
- Section 9 API surface: Task 11.
- Section 10 data model: Tasks 3, 7, 8.
- Section 11 frontend UX: Tasks 12 to 15.
- Section 12 demo path: Task 6 (`DemoReportPipeline`) and Task 8 (`DemoFallbackEnabled`).
- Section 13 events: Task 8 and Task 15 (`agent-stream.service.ts` event names).
- Section 14 testing: every task.
- Section 15 acceptance criteria: covered by Tasks 8, 9, 11, 13, 14.

**Placeholder scan:** no `TBD`, `TODO`, or "similar to Task N" references.

**Type consistency:** `ConversationTurn.Step` and `.Control` are strings; the template compares `'manageremail'` for the manager step; `ReportIntake.Email`/`.Csv` are used by both the orchestrator and tests. `ColumnCatalog` returns `ColumnOption`; the CSV endpoint reads `ColumnOption.Name`.
