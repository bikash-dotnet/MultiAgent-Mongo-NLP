# Sprint 3 — Semantic Kernel Orchestrator and Complex MQL Synthesis

**Duration:** 2 weeks  
**Sprint goal:** Introduce the Semantic Kernel multi-agent orchestrator and generate validated aggregation pipelines for complex English queries.

**BRD version:** 4.2  
**Depends on:** Sprint 2

---

## Traceability

| BRD ID | Priority | Coverage in this sprint |
| --- | --- | --- |
| BRD-FR-03 | Critical | NL → validated MQL via few-shot templates |
| BRD-NFR-02 | — | Complex NVIDIA LLM MQL generation 1.5–3 s |
| BRD-NFR-06 | — | Self-correct up to 3 syntax-validation attempts |
| BRD-NFR-07 | — | Max-1 clarification question per turn |
| BRD-NFR-13 | — | Paused-state persistence hook (store interface; governance pause in Sprint 5) |

---

## Tech stack

- Microsoft Semantic Kernel process / agent framework
- NVIDIA NIM connector
- Prompt assets: `prompt_template.txt`, `schema.txt`, `sample.txt`, `examples.txt`
- Orchestrator agent + Hybrid Intent & Query Generator agent

---

## Stories

### S3-01 Orchestrator agent (supervisor)
- Session identity already from Sprint 1
- Max-1 Clarification Rule: never more than one follow-up per turn
- Silent fallbacks: `limit(10)`, `sort: rating_desc`, `market: All`
- Stream agent status events over existing SSE (`agent.started`, `agent.clarifying`, `agent.completed`)

### S3-02 Query generator agent
- Invoke NVIDIA NIM **only** when Sprint 2 router returns `COMPLEX_LLM_REQUIRED`
- Build prompts from `prompt_template.txt` + `schema.txt` + few-shot `sample.txt` / `examples.txt`
- Produce MongoDB aggregation pipeline JSON

### S3-03 Self-correction loop
- On syntax validation error, retry up to **3** times
- After 3 failures, surface a user-visible error (do not loop)
- Record attempt count in agent telemetry

### S3-04 Schema type checks (light)
- Confirm numeric `price`, array `amenities` against `schema.txt`
- Full AST whitelist and write-block is Sprint 4

### S3-05 Paused-state store interface
- `IAgentStateStore` save/load for governance holds
- In-memory implementation this sprint; Mongo persistence in Sprint 6

---

## Acceptance criteria

- [ ] Complex English queries produce aggregation pipelines using few-shot templates (BRD-FR-03)
- [ ] Simple queries still skip NIM (regression on Sprint 2)
- [ ] Syntax failures retry at most 3 times then alert the user (BRD-NFR-06)
- [ ] Orchestrator never asks more than one question per turn (BRD-NFR-07)
- [ ] `"just run it"` applies silent defaults
- [ ] SPA shows streamed agent activity for the query path

---

## Out of scope

- Write-operator blocking and field flags (Sprint 4)
- Mongo execution (Sprint 6)
- Narrative insights / export (Sprint 7)

---

## Exit artifacts

- Semantic Kernel process graph (Orchestrator + Query Agent)
- Prompt/schema/few-shot files under `assets/prompts/`
- `IAgentStateStore` abstraction
