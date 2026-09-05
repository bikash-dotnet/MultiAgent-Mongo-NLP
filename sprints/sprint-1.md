# Sprint 1 — Platform Foundation, Auth, and Greeting Engine

**Duration:** 2 weeks  
**Sprint goal:** Stand up the Angular 21 SPA and ASP.NET Core 10 gateway with JWT auth, SSE plumbing, and the time-aware personalized greeting.

**BRD version:** 4.2  
**Depends on:** none

---

## Traceability

| BRD ID | Priority | Coverage in this sprint |
| --- | --- | --- |
| BRD-FR-01 | High | Dynamic, time-aware greeting from authenticated session |
| BRD-NFR-11 | — | SSE transport skeleton (client `EventSource` + server `IAsyncEnumerable`) |
| BRD-NFR-12 | — | JWT session identity on every request |

---

## Tech stack (locked by BRD 4.2)

- Client: Angular 21 standalone components, Angular Material / PrimeNG
- Gateway: ASP.NET Core 10 (`net10.0`) Minimal APIs
- Auth: `Microsoft.AspNetCore.Authentication.JwtBearer`
- Contracts: FluentValidation + DataAnnotations
- Clock: `TimeProvider` (no LLM calls for greetings)

---

## Stories

### S1-01 Solution scaffold
- Create `src/gateway` ASP.NET Core 10 Web API
- Create `src/web` Angular 21 SPA
- Configure HTTPS, CORS, and reverse-proxy of `/api` from the SPA dev server to the gateway
- Add `appsettings.json` placeholders for MongoDB, JWT, NVIDIA NIM (no secrets committed)

### S1-02 JWT auth and RBAC claims
- Validate JWT on all `/api/*` routes except health
- Map claims: `user_id`, `name`, `email`, `role`, `lead_user_id`
- Roles: Business Analyst, Team Lead, Engineering Manager, Data Owner / Admin
- Reject missing/expired tokens with `401`

### S1-03 Greeting engine (BRD-FR-01)
- `GET /api/session/greeting` returns `{ displayName, period, message, chips[] }`
- Period from `TimeProvider` (`morning` / `afternoon` / `evening`)
- Message format: `Hi {name}, Good {period}`
- Zero model calls; latency target < 1 ms server-side (BRD-NFR-01)
- SPA renders greeting + quick-action chips on load

### S1-04 SSE skeleton
- `GET /api/agents/stream` emits named SSE events (`agent.idle` heartbeat)
- Angular `EventSource` service reconnects on drop
- No agent graph yet; stream proves transport only

### S1-05 Health and observability baseline
- `GET /health` (anonymous)
- Structured logging with `session_id` correlation header

---

## Acceptance criteria

- [x] Authenticated user sees a time-aware greeting with their session name and quick-action chips (BRD-FR-01)
- [x] Unauthenticated requests to greeting/stream return `401`
- [x] Greeting path does not call NVIDIA NIM
- [x] SPA can open an SSE connection to the gateway and receive a heartbeat
- [x] `/api` from the SPA is reverse-proxied to the gateway

---

## Out of scope

- Semantic cache, slot filling, MQL, governance, MongoDB queries, exports

---

## Exit artifacts

- Runnable gateway + SPA
- JWT-protected greeting endpoint
- SSE client/server stub
