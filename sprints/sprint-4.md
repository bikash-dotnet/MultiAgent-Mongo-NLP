# Sprint 4 — Guardrails: MQL AST and Field-Flag Governance

**Duration:** 2 weeks  
**Sprint goal:** Reject unsafe MQL and pause any query that targets sensitive registry fields.

**BRD version:** 4.2  
**Depends on:** Sprint 3

---

## Traceability

| BRD ID | Priority | Coverage in this sprint |
| --- | --- | --- |
| BRD-FR-04 | Critical | Read-only syntax; block write/admin operators |
| BRD-FR-05 | Critical | Verify fields against `schema_field_registry` |
| BRD-NFR-08 | — | Block `$out`, `$merge`, `drop`, `deleteMany` |
| BRD-NFR-01 | — | AST / flag verification < 1 ms |

---

## Tech stack

- C# MQL AST walker + rule engine
- MongoDB `schema_field_registry` (seed data this sprint)
- Guardrail & Security Agent
- Schema & Semantic Validator Agent (complete type + whitelist pass)

---

## Stories

### S4-01 MQL AST parser
- Parse aggregation pipeline / filter documents into a walkable tree
- Field-path extraction for every referenced attribute
- Latency target < 1 ms for typical pipelines

### S4-02 Read-only enforcement (BRD-FR-04)
- Block stages/operators: `$out`, `$merge`, `drop`, `deleteMany`
- Block any non-read command
- Fail closed: unparseable query is rejected

### S4-03 Schema whitelist
- Compatibility check against `schema.txt`
- Unknown field paths rejected unless explicitly allowed

### S4-04 Field sensitivity registry (BRD-FR-05)
- Seed `schema_field_registry` including `address.location.coordinates` and host verification fields
- Flags: `is_sensitive`, `requires_approval`, `data_owner_roles`
- If `requires_approval` and no exemption yet: halt and emit `governance.paused` SSE event
- Create `access_requests` stub with status `PENDING_LEAD` (approval UX in Sprint 5)

### S4-05 Guardrail agent in the SK graph
- Runs after query generation, before execution
- Unit tests: write ops rejected; sensitive geo query paused; clean amenity search passes

---

## Acceptance criteria

- [ ] Any write/admin operator is rejected before execution (BRD-FR-04)
- [ ] Queries touching flagged fields pause unless later exempt (BRD-FR-05)
- [ ] Clean read queries with non-sensitive fields pass the AST
- [ ] Unparseable MQL is rejected (fail closed)
- [ ] AST/flag step stays under 1 ms in bench tests

---

## Out of scope

- Data Owner auto-approve and manager override (Sprint 5)
- Actual Mongo `find`/`aggregate` (Sprint 6)

---

## Exit artifacts

- Guardrail & Schema Validator agents
- Seeded `schema_field_registry`
- Pause signal + `PENDING_LEAD` request document shape
