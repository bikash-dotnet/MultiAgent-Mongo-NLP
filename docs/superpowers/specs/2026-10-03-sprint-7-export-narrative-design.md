# Sprint 7 Export, Narrative, and Admin Analytics Design

**Status:** Approved for implementation
**BRD:** v4.2
**Sprint:** `sprints/sprint-7.md`
**Depends on:** Sprint 6 (execution runner, dual ingestion, immutable audit)

## 1. Goal

Deliver the BRD-FR-09 and BRD-FR-11 surface on top of the Sprint 6 execution and
audit foundation: an in-memory export path (CSV, XLSX, SMTP PDF), a deterministic
zero-token narrative briefing, and an admin analytics dashboard aggregated from the
append-only `audit_logs` collection.

## 2. Traceability

| BRD ID | Priority | Design decision |
| --- | --- | --- |
| BRD-FR-09 | High | `Gateway.Analytics.AdminAnalytics.Aggregate` reads `IAuditLogStore.ListAsync` and returns immutable metrics; `GET /api/admin/analytics`; SPA `/admin` route polls every 10 s |
| BRD-FR-11 | High | `Gateway.Reports` gains an XLSX writer, a PDF briefing renderer, and a MailKit SMTP dispatcher, all operating on `MemoryStream` |
| BRD-NFR-04 | — | `NarrativeInsights` is a pure C# string formatter; it never calls NVIDIA and consumes zero tokens |
| BRD-NFR-10 | — | No export code path opens a file; every artifact is a `byte[]` produced from an in-memory stream |
| BRD-NFR-09 | — | Analytics and export only read `audit_logs`; no update/delete API is added |

## 3. Architecture

```text
SPA /admin ──GET /api/admin/analytics──> AdminAnalytics ──> IAuditLogStore.ListAsync
                                                  (read-only aggregation)

SPA results ──GET /api/conversations/{id}/report.{csv,xlsx}──> ExportDeliveryService
SPA results ──POST /api/conversations/{id}/report/briefing───> NarrativeInsights
SPA results ──POST /api/conversations/{id}/report/email─────> PdfBriefingRenderer
                                                              └─> IEmailSender (MailKit)
```

The export and briefing paths reuse the `ConversationState.Execution` tabular payload
that Sprint 6 already persists, so no new execution or audit concerns are introduced.
`ExportDeliveryService` is transport-agnostic: it returns `byte[]` for CSV/XLSX/PDF and
delegates to `IEmailSender` for SMTP. `IEmailSender` is a seam so tests never open a
socket; the production adapter is `MailKitEmailSender`.

## 4. Data contracts

`Gateway.Analytics`:

```csharp
public sealed record AdminAnalyticsMetrics(
    int TotalExecutions,
    int SemanticCacheHits,
    double SemanticCacheHitRate,
    int TotalLlmTokens,
    int SensitiveAccessCount,
    int OverrideCount,
    double P95DurationMs,
    IReadOnlyDictionary<string, int> ExportCounts,
    IReadOnlyDictionary<string, int> DataSourceCounts,
    DateTimeOffset? LastExecutionAt);
```

`Gateway.Reports`:

```csharp
public sealed record BriefingRequest(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows);

public sealed record BriefingResult(string Text, bool GeneratedByLlm);

public sealed record ExportResult(string Format, byte[] Content, string ContentType, string FileName);
```

Export formats are the strings `CSV`, `XLSX`, `PDF`, matching the `export_format` value
already written to `audit_logs` by `AuditLogFactory`.

## 5. In-memory guarantees (BRD-NFR-10)

- `XlsxExporter` uses `ClosedXML` and serializes into a `MemoryStream`; the `IXLWorkbook`
  and stream are disposed inside the call and only `byte[]` escapes.
- `PdfBriefingRenderer` uses `QuestPDF` with `Document.Create` and `GeneratePdf()` into a
  `MemoryStream`. The QuestPDF Community license is set once at startup.
- `MailKitEmailSender` builds a `MimeMessage` with a stream-backed body part.
- Tests assert no file is created under the persistence data directory during export.

## 6. Analytics semantics

- `TotalExecutions` = number of `audit_logs` documents.
- `SemanticCacheHitRate` = hits / total, `0` when there are no rows.
- `TotalLlmTokens` = sum of `NlpPerformance.LlmTokensConsumed`.
- `SensitiveAccessCount` = documents where `Governance.SensitiveDataAccessed`.
- `OverrideCount` = documents where `Governance.OverrideInvoked`.
- `P95DurationMs` = nearest-rank 95th percentile of `NlpPerformance.ExecutionDurationMs`
  (index `ceil(0.95 * n) - 1` on the ascending-sorted list); `0` when empty.
- `ExportCounts` groups `ExecutionDetails.ExportFormat` where non-null.
- `DataSourceCounts` groups the top-level `DataSource`.
- `LastExecutionAt` is the maximum `AuditTimestamp`, null when empty.

Aggregation is pure and deterministic; it never mutates the store.

## 7. API surface

| Method | Route | Auth | Behaviour |
| --- | --- | --- | --- |
| GET | `/api/admin/analytics` | JWT | Returns `AdminAnalyticsMetrics` (JSON) |
| GET | `/api/conversations/{id}/report.xlsx` | JWT | 200 `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`; 404/409 as CSV path |
| POST | `/api/conversations/{id}/report/briefing` | JWT | Returns `{ text, generatedByLlm }`; standard briefing is deterministic |
| POST | `/api/conversations/{id}/report/email` | JWT | Renders PDF and dispatches via `IEmailSender`; returns `{ sent, recipient, format }` |

The existing `/api/conversations/{id}/report.csv` route is retained unchanged.

## 8. SPA

- `models/analytics.ts` mirrors `AdminAnalyticsMetrics`.
- `services/analytics.service.ts` exposes `metrics()` and is polled by the component.
- `admin/admin-analytics.component` renders metric cards, export/data-source breakdowns,
  and a live refresh timestamp; route `/admin`.
- The component tolerates an empty audit log (all zeros) and never writes.

## 9. Testing strategy

Backend xUnit: aggregator math (empty, cache hit rate, p95, overrides), XLSX round-trip
via `XLWorkbook`/zip signature, PDF magic bytes `%PDF-`, briefing determinism, SMTP seam
capture, and API contracts using `GatewayFactory`.

SPA vitest: service URL/verb assertions with `HttpTestingController`, component renders
metrics and tolerates the empty state.

## 10. Out of scope

- Write/mutate operations against listings (BRD §14).
- Persisting export artifacts to disk.
- Direct client access to MongoDB or NVIDIA.
