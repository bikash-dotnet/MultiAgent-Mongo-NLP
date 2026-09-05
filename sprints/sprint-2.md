# Sprint 2 — Hybrid NLP: Cache, Slots, and Simple MQL

**Duration:** 2 weeks  
**Sprint goal:** Route routine English queries through local deterministic NLP so simple searches never hit the LLM.

**BRD version:** 4.1  
**Depends on:** Sprint 1

---

## Traceability

| BRD ID | Priority | Coverage in this sprint |
| --- | --- | --- |
| BRD-FR-02 | Critical | Cache lookup and slot-filling before NVIDIA LLM |
| BRD-NFR-01 | — | Slot extract < 5 ms; cache < 10 ms; simple MQL < 2 ms |
| BRD-NFR-04 | — | Zero token cost on cache / slots / simple MQL |
| BRD-NFR-05 | — | Semantic cache hit when cosine > 0.95 |

---

## Tech stack

- Embeddings: ONNX Runtime + local `bge-small`
- Slots: `System.Text.RegularExpressions` + gazetteer dictionaries
- Simple MQL: Scriban templates
- Intent filter: C# rule engine (Search / Export / Clarify)

---

## Stories

### S2-01 Semantic cache
- Persist previously **approved** query embeddings (in-memory for now; Mongo later if needed)
- Cosine similarity threshold **> 0.95**
- On HIT: return cached MQL + metadata `semantic_cache_hit = true`
- Latency target < 10 ms

### S2-02 Slot extraction
- Extract known schema fields: city/market, rooms/beds, amenities, price bounds
- Gazetteers loaded from config (amenities list, market names)
- Latency target < 5 ms
- Record `slot_extraction_used` for audit payload (written in Sprint 6)

### S2-03 Rule-based intent filter
- Classify: Search, Export, or Clarify
- Enforce Max-1 Clarification Rule at the filter boundary (full orchestrator in Sprint 3)
- Silent defaults if user says `"just run it"` or skips non-critical slots:
  - `limit(10)`, `sort: rating_desc`, `market: All`

### S2-04 Simple MQL builders
- Scriban templates for `$match` / `$limit` / `$sort` on extracted slots
- No NVIDIA call on this path
- Latency target < 2 ms

### S2-05 NLP routing service
- Pipeline: cache → slots → intent → simple MQL **or** `COMPLEX_LLM_REQUIRED`
- Token counter stays `0` unless later sprints invoke NIM
- Unit tests for HIT/MISS, slot maps, and default injection

---

## Acceptance criteria

- [ ] Known/simple queries resolve via cache or slot/template path with **zero LLM tokens** (BRD-FR-02)
- [ ] Cache hit only when cosine > 0.95 (BRD-NFR-05)
- [ ] `"listings with pools in Los Angeles, just run it"` produces MQL without NVIDIA
- [ ] Latency budgets in BRD-NFR-01 hold under unit/bench tests for cache, slots, simple MQL

---

## Out of scope

- NVIDIA few-shot MQL generation (Sprint 3)
- AST guardrails (Sprint 4)
- Execution against MongoDB (Sprint 6)

---

## Exit artifacts

- `INlpRouter` with cache / slot / template paths
- Gazetteer + Scriban template assets
- Bench numbers for NFR-01 local steps
