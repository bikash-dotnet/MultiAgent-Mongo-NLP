# Sprint 7 — In-Memory Export, Narrative Insights, and Admin Analytics

**Duration:** 2 weeks  
**Sprint goal:** Deliver business briefings, in-memory exports, SMTP PDF dispatch, and the admin dashboard from `audit_logs`.

**BRD version:** 4.2  
**Depends on:** Sprint 6

---

## Traceability

| BRD ID | Priority | Coverage in this sprint |
| --- | --- | --- |
| BRD-FR-09 | High | Real-time metrics from `audit_logs` |
| BRD-FR-11 | High | CSV, XLSX, SMTP PDF; no disk writes |
| BRD-NFR-04 | — | Standard executive summary is deterministic / zero-token |
| BRD-NFR-10 | — | Export path in-memory only |

---

## Tech stack

- Narrative Insights Agent: deterministic C# string formatter for standard summaries; NVIDIA NIM only for qualitative summaries on request
- Export & Delivery Agent: CsvHelper, ClosedXML, QuestPDF, MailKit
- Angular Admin Executive Analytics Dashboard

---

## Stories

### S7-01 Narrative Insights Agent
- Standard briefing: counts, median prices, top locations via templates (zero tokens)
- Optional qualitative summary: NVIDIA path, still AST-checked if it re-queries
- Stream briefing text over SSE

### S7-02 In-memory export (BRD-FR-11)
- CSV and XLSX generated in `MemoryStream` / `RecyclableMemoryStream`
- No temp files under any success or failure path
- Browser download from stream

### S7-03 SMTP PDF briefing
- QuestPDF in memory
- MailKit transactional send
- Audit `export_format` (`CSV` / `XLSX` / `PDF`)

### S7-04 Admin analytics (BRD-FR-09)
- Aggregate `audit_logs`: cache hit rate, token spend, sensitive-access count, override count, p95 duration
- Angular admin module with live refresh
- Read-only aggregations; never mutate `audit_logs`

### S7-05 End-to-end hardening
- Regression across Sprints 1–6
- Confirm NFR latency/cost/timeout still hold on the happy path

---

## Acceptance criteria

- [x] CSV/XLSX/PDF export completes in memory; no disk temp files; PDF can be emailed via SMTP (BRD-FR-11)
- [x] Admin dashboard shows live metrics derived from `audit_logs` (BRD-FR-09)
- [x] Standard executive summary does not call NVIDIA
- [x] Export and briefing events appear on the corresponding `audit_logs` row
- [ ] Full FR set BRD-FR-01 … BRD-FR-11 demonstrable on a staging environment (deferred: Sprint 8 deployment was skipped)

---

## Out of scope (platform, per BRD §14)

- Write/mutate operations against listings
- Unrestricted multi-turn clarification
- Direct client access to MongoDB or NVIDIA

---

## Exit artifacts

- Narrative + Export agents
- Admin analytics dashboard
- Production-ready demo path covering all BRD FRs

**Status:** Implemented and verified against the code. `Gateway.Analytics.AdminAnalytics.Aggregate`
reads `IAuditLogStore` read-only and `GET /api/admin/analytics` feeds the SPA `/admin` dashboard;
`ExportDeliveryService` builds CSV/XLSX/PDF entirely in memory (`ClosedXML`, `QuestPDF`) and dispatches
briefings through the `MailKit`-backed `IEmailSender` seam (`GET report.csv`, `GET report.xlsx`,
`POST report/briefing`, `POST report/email`); `NarrativeInsights.Standard` is a deterministic,
zero-token formatter. The `export_format` value is written to the execution's `audit_logs` row by
`AuditLogFactory` via `ExecutionRequest.ExportFormat`. Tests: `Reports/*`, `Analytics/AdminAnalyticsTests`,
`ApiExportTests`, `ApiAnalyticsTests`. Design and plan: `docs/superpowers/specs|plans/2026-10-03-sprint-7-export-narrative*`.
The staging-environment acceptance item is deferred because Sprint 8 (deployment) was skipped.
