# Sprint 7 Export, Narrative, and Admin Analytics Implementation Plan

> **For agentic workers:** Implement task-by-task, TDD-first. Steps use `- [ ]` checkboxes.

**Goal:** Ship BRD-FR-09 (admin analytics from `audit_logs`) and BRD-FR-11 (in-memory CSV/XLSX/PDF export plus SMTP dispatch) with a deterministic narrative briefing.

**Architecture:** Add `Gateway.Analytics` for read-only aggregation and extend `Gateway.Reports` with an XLSX writer, a PDF briefing renderer, an SMTP seam, and an `ExportDeliveryService`. Extend the SPA with an `/admin` analytics route. Reuse the Sprint 6 `ConversationState.Execution` payload and `IAuditLogStore`.

**Tech Stack:** ASP.NET Core 10 minimal API, `ClosedXML`, `QuestPDF`, `MailKit`, xUnit; Angular 21 standalone components, vitest + jsdom.

## Global Constraints

- Runtime floor `net10.0`; do not change the target framework.
- New NuGet packages limited to `ClosedXML`, `QuestPDF`, `MailKit` on `src/gateway`.
- No new npm dependency; the SPA uses the existing Angular and vitest stack.
- Export formats are wire strings `CSV`, `XLSX`, `PDF`.
- Every export path is in-memory only; never write a file (BRD-NFR-10).
- Analytics never mutates `audit_logs`; no update/delete method is introduced.
- No code comments and no emojis.
- Do not commit `src/web/.vscode/`; keep `NvidiaNim:ApiKey` as the existing `TBD` placeholder.
- Prefix every .NET shell invocation with `export PATH="$PATH:/root/.dotnet"`.
- Running the full .NET suite rewrites `docs/benchmarks/*.md`; restore them afterwards and never stage them.
- Commit with the repo hook workaround and co-author trailer:
  `printf '<subject>\n\nCo-authored-by: monkeycode-ai <monkeycode-ai@chaitin.com>\n' > /tmp/msg && git -c coauthor.0.name= -c coauthor.0.email= commit -F /tmp/msg`

## File Structure

New backend files:

- `src/gateway/Analytics/AdminAnalyticsMetrics.cs`
- `src/gateway/Analytics/AdminAnalytics.cs`
- `src/gateway/Reports/XlsxExporter.cs`
- `src/gateway/Reports/ExportResult.cs`
- `src/gateway/Reports/NarrativeInsights.cs`
- `src/gateway/Reports/BriefingResult.cs`
- `src/gateway/Reports/IEmailSender.cs`
- `src/gateway/Reports/MailKitEmailSender.cs`
- `src/gateway/Reports/PdfBriefingRenderer.cs`
- `src/gateway/Reports/ExportDeliveryService.cs`
- `src/gateway/Reports/ReportsOptions.cs` (extended with SMTP settings)

New SPA files:

- `src/web/src/app/models/analytics.ts`
- `src/web/src/app/services/analytics.service.ts` / `.spec.ts`
- `src/web/src/app/admin/admin-analytics.component.ts` / `.html` / `.scss` / `.spec.ts`

New test files:

- `tests/Gateway.Tests/Analytics/AdminAnalyticsTests.cs`
- `tests/Gateway.Tests/Reports/XlsxExporterTests.cs`
- `tests/Gateway.Tests/Reports/NarrativeInsightsTests.cs`
- `tests/Gateway.Tests/Reports/PdfBriefingRendererTests.cs`
- `tests/Gateway.Tests/Reports/EmailDispatchTests.cs`
- `tests/Gateway.Tests/ApiAnalyticsTests.cs`
- `tests/Gateway.Tests/ApiExportTests.cs`

Changed files: `src/gateway/gateway.csproj`, `src/gateway/appsettings.json`, `src/gateway/Nlp/NlpServiceCollectionExtensions.cs`, `src/gateway/Program.cs`, `src/web/src/app/app.routes.ts`, `README.md`.

---

### Task 1: Admin analytics aggregation

**Files:** create `src/gateway/Analytics/AdminAnalyticsMetrics.cs`, `AdminAnalytics.cs`; test `tests/Gateway.Tests/Analytics/AdminAnalyticsTests.cs`.

- [ ] Step 1: Write failing tests covering empty input, cache hit rate, token sum, sensitive/override counts, p95, export and data-source groupings, and last timestamp.
- [ ] Step 2: Run `dotnet test --filter FullyQualifiedName~AdminAnalyticsTests`, expect a missing-type build failure.
- [ ] Step 3: Implement the immutable metrics record and the pure `Aggregate(IReadOnlyList<AuditLogDocument>)` function.
- [ ] Step 4: Re-run, expect PASS.
- [ ] Step 5: Commit `feat(analytics): aggregate audit_logs into admin metrics`.

### Task 2: Admin analytics endpoint

**Files:** modify `NlpServiceCollectionExtensions.cs`, `Program.cs`; test `tests/Gateway.Tests/ApiAnalyticsTests.cs`.

- [ ] Step 1: Write a failing API test asserting `GET /api/admin/analytics` returns 200 with zeroed metrics and requires auth.
- [ ] Step 2: Implement the endpoint over `IAuditLogStore.ListAsync` and register the service.
- [ ] Step 3: Verify PASS, commit `feat(analytics): expose the admin analytics endpoint`.

### Task 3: In-memory XLSX export

**Files:** create `src/gateway/Reports/XlsxExporter.cs`, `ExportResult.cs`; modify `gateway.csproj`; test `tests/Gateway.Tests/Reports/XlsxExporterTests.cs`.

- [ ] Step 1: Add the `ClosedXML` package.
- [ ] Step 2: Write failing tests that export rows and reopen the bytes with `XLWorkbook`, asserting header and cell values, and assert the zip signature.
- [ ] Step 3: Implement `XlsxExporter.Export` into a `MemoryStream`.
- [ ] Step 4: Verify PASS, commit `feat(reports): add in-memory xlsx export`.

### Task 4: Deterministic narrative briefing

**Files:** create `src/gateway/Reports/NarrativeInsights.cs`, `BriefingResult.cs`; test `tests/Gateway.Tests/Reports/NarrativeInsightsTests.cs`.

- [ ] Step 1: Write failing tests for counts, median numeric column, top market, and empty input.
- [ ] Step 2: Implement the pure formatter; `GeneratedByLlm` is always false on this path.
- [ ] Step 3: Verify PASS, commit `feat(reports): add deterministic narrative briefing`.

### Task 5: PDF briefing and SMTP dispatch

**Files:** create `PdfBriefingRenderer.cs`, `IEmailSender.cs`, `MailKitEmailSender.cs`, `ExportDeliveryService.cs`; modify `ReportsOptions.cs`, `gateway.csproj`, `NlpServiceCollectionExtensions.cs`; tests `PdfBriefingRendererTests.cs`, `EmailDispatchTests.cs`.

- [ ] Step 1: Add `QuestPDF` and `MailKit`; set the QuestPDF Community license at composition.
- [ ] Step 2: Write failing tests asserting the PDF bytes start with `%PDF-` and that `ExportDeliveryService` records the recipient through the `IEmailSender` seam.
- [ ] Step 3: Implement the renderer, seam, MailKit adapter, and delivery service.
- [ ] Step 4: Verify PASS, commit `feat(reports): add in-memory pdf briefing and smtp dispatch`.

### Task 6: Export and briefing endpoints

**Files:** modify `Program.cs`; test `tests/Gateway.Tests/ApiExportTests.cs`.

- [ ] Step 1: Write failing API tests for `report.xlsx`, `report/briefing`, and `report/email`.
- [ ] Step 2: Implement the three endpoints over `ConversationOrchestrator.GetAsync` and `ExportDeliveryService`.
- [ ] Step 3: Verify PASS, commit `feat(reports): expose xlsx briefing and email endpoints`.

### Task 7: Angular admin analytics dashboard

**Files:** create `models/analytics.ts`, `services/analytics.service.ts` / `.spec.ts`, `admin/admin-analytics.component.*`; modify `app.routes.ts`; test component spec.

- [ ] Step 1: Write failing service and component tests.
- [ ] Step 2: Implement the model, service, and standalone component; register `/admin`.
- [ ] Step 3: Run `npm test`, verify PASS, commit `feat(web): add the admin analytics dashboard`.

### Task 8: Hardening

- [ ] Step 1: Run the full gateway and web suites; fix regressions.
- [ ] Step 2: Restore `docs/benchmarks/`, update `README.md`, and commit `docs(sprint-7): document export and analytics`.

## Acceptance criteria

- [ ] Aggregation returns deterministic metrics from `audit_logs` and never mutates it.
- [ ] XLSX and PDF are produced fully in memory; tests confirm no disk artifacts.
- [ ] A standard briefing is deterministic and does not touch NVIDIA.
- [ ] `/api/admin/analytics` and the export endpoints honour JWT auth.
- [ ] The SPA `/admin` route renders live metrics and tolerates an empty audit log.
