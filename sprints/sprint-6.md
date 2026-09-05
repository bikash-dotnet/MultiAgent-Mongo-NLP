# Sprint 6 — Execution Runner, Dual Ingestion, and Immutable Audit

**Duration:** 2 weeks  
**Sprint goal:** Execute approved read-only queries against MongoDB or Enterprise Core REST and write a complete audit row every time.

**BRD version:** 4.2  
**Depends on:** Sprint 5

---

## Traceability

| BRD ID | Priority | Coverage in this sprint |
| --- | --- | --- |
| BRD-FR-08 | Critical | Immutable `audit_logs` on every execution |
| BRD-FR-10 | High | MongoDB driver **or** Enterprise Core REST |
| BRD-NFR-03 | — | Query timeout maximum 5,000 ms |
| BRD-NFR-09 | — | `audit_logs` append-only |
| BRD-NFR-13 | — | Durable paused-state persistence (Mongo) |

---

## Tech stack

- `MongoDB.Driver` against `sample_airbnb.listingsAndReviews`
- HttpClient typed client for `ENTERPRISE_CORE_API_V2`
- Execution Runner Agent
- `audit_logs` append-only collection (no update/delete APIs)

---

## Stories

### S6-01 Execution Runner
- Run only after Guardrail pass **and** (no sensitive flags **or** `APPROVED` / owner exemption)
- Timeout: **5,000 ms** (`CancellationToken`); fail with timeout error, still audit
- Return tabular JSON to the SPA grid

### S6-02 Dual ingestion (BRD-FR-10)
- Route key (`data_source`): MongoDB driver **or** Enterprise Core REST
- Client does not choose the transport; gateway policy does
- Same result contract for both paths

### S6-03 Immutable audit trail (BRD-FR-08)
- Insert-only `audit_logs` matching BRD section 6.1:
  - user, nlp_performance, request_details, execution_details, governance
- Include `semantic_cache_hit`, `slot_extraction_used`, `llm_tokens_consumed`, `execution_duration_ms`
- Include `flags_triggered`, `exemption_type`, `override_invoked`, `authorized_by`
- No update/delete commands exposed for this collection

### S6-04 Durable agent state
- Replace in-memory `IAgentStateStore` with Mongo-backed store
- Governance holds survive process recycle

### S6-05 Results grid
- Angular Material / PrimeNG grid bound to execution payload
- Stream `agent.executing` / `agent.completed` SSE events

---

## Acceptance criteria

- [ ] Every execution writes a complete `audit_logs` document (BRD-FR-08)
- [ ] Execution Runner can target MongoDB or Enterprise Core REST without client change (BRD-FR-10)
- [ ] Queries exceeding 5,000 ms are cancelled and audited
- [ ] `audit_logs` has no update/delete path (BRD-NFR-09)
- [ ] Sensitive query after approval returns listing rows
- [ ] Paused state survives gateway restart (BRD-NFR-13)

---

## Out of scope

- CSV/XLSX/PDF export (Sprint 7)
- Admin analytics aggregations (Sprint 7)

---

## Exit artifacts

- Execution Runner agent (Mongo + REST)
- Append-only `audit_logs`
- SPA results grid
