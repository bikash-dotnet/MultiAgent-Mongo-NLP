# Sprint 9 — UI Foundation, App Shell, and Chat Experience

**Duration:** 2 weeks
**Sprint goal:** Adopt PrimeNG as the single component framework, deliver a cohesive admin shell, and rebuild the workspace as an AdminLTE-style chat experience wired to the existing NLP, conversation, and SignalR services.

**BRD version:** 4.2
**Depends on:** Sprint 8

---

## Traceability

These sprints are UI/API enhancements layered on the BRD feature set; they trace to BRD sections and NFRs rather than new FR IDs.

| BRD ID / § | Coverage in this sprint |
| --- | --- |
| BRD §3.1 | PrimeNG adopted as the Angular 21 component framework; standalone components preserved |
| BRD §9.1–9.4 | Session start, conversation, results, and governance UX expressed in the new shell |
| BRD-NFR-07 | Chat composer enforces at most one clarification question per turn |
| BRD-NFR-11 | Agent activity and results surface over the existing SignalR hub |
| BRD-NFR-12 | JWT session identity unchanged; auth interceptor reused |

---

## Tech stack

- `primeng` + `@primeuix/themes` (Aura preset), pinned to the Angular 21 major
- Angular 21 standalone components, reactive forms, lazy-loaded feature routes
- Existing `SessionService`, `ConversationService`, `NlpQueryService`, `AgentStreamService` (no data-layer rewrite)
- No AdminLTE/jQuery dependency; the AdminLTE v4 chat page is a visual reference only

---

## Design reference

Chat layout references `https://adminlte.io/themes/v4/pages/chat.html`:

- **Center:** scrolling message thread, incoming (agent) and outgoing (user) bubbles, timestamps,
  typing/agent-activity indicator, composer with quick-action chips.
- **Right rail:** case context for the active conversation — intent, slots applied, report intake
  progress, governance/approval status, export actions.
- **Left contacts/search column is explicitly out of scope** and must not be reproduced.

---

## Stories

### S9-01 PrimeNG integration and theming
- Add `primeng` + `@primeuix/themes`; register the Aura preset and dark-mode tokens
- `providePrimeNG(...)` plus `provideAnimationsAsync()` and overlay services (Message, Confirmation, Dialog) in `app.config.ts`
- Import components per standalone component; no global module imports
- Set/adjust `angular.json` production budgets with lazy loading so the initial bundle stays under the error threshold
- Confirm `src/web/angular.json` `serve.options.allowedHosts` (`.monkeycode-ai.live`) and `proxy.conf.json` remain intact

### S9-02 App shell and navigation
- Replace the bare `app.component.html` nav with a shell: collapsible sidebar (`p-panelMenu`), topbar with product name, user avatar/name, governance toggle, and theme switch
- Sidebar entries: Chat (`/`), Governance (`/governance`), Admin (`/admin`), Schemas (`/schemas`), Configuration (`/configuration`), Business Rules (`/business-rules`)
- New routes are lazy-loaded and initially render a placeholder until Sprints 10–12 land
- Governance toggle moves from the workspace into the topbar; keep `PUT /api/governance/approval` behavior

### S9-03 Chat experience (AdminLTE reference)
- Rebuild `workspace.component` as the chat page: message thread + right context rail + composer
- Outgoing user bubble calls the existing conversation/query flow; incoming agent bubble renders
  greeting, clarification questions, and results summaries
- Quick-action chips from `GET /api/session/greeting` populate the composer
- Clarification is presented as a single inline question and answer control (BRD-NFR-07)
- Results render in the existing `results-grid` component embedded in the thread; export buttons
  reuse the CSV/XLSX/PDF endpoints

### S9-04 Agent activity and streaming UX
- Consume `AgentStreamService` to show live agent state: idle, started, executing, clarifying,
  completed, plus governance/report events
- Typing indicator and per-agent activity strip; subscribe to all 16 gateway event names
- Graceful degraded state when SignalR is unavailable (banner, no infinite spinner)

### S9-05 Responsive, accessibility, and polish
- Responsive breakpoints: sidebar collapses under `lg`, right rail stacks under `md`
- Keyboard navigation, focus management, `aria-live` on the message thread, color-contrast check on Aura tokens
- Empty, loading, and error states for the thread, rail, and results

### S9-06 Tests
- Component tests for shell navigation, chat composer, message rendering, and stream status mapping
- Keep `npm test` green; add a smoke test for lazy-route resolution
- Manual a11y/keyboard pass documented in the sprint notes

---

## Acceptance criteria

- [x] PrimeNG (Aura) is the only component framework; no AdminLTE/jQuery assets in the bundle
- [x] App shell provides sidebar navigation to Chat, Governance, Admin, Schemas, Configuration, Business Rules
  (sidebar entries in `app.component.ts:18`; topbar with governance toggle in `workspace.component.html`;
  theme switch referenced via `.app-dark` Aura dark-mode selector)
- [x] New feature routes are lazy-loaded; `ng build --configuration production` succeeds
  (initial bundle 774 kB, under the 1 MB error budget; 274 kB over the 500 kB warning budget —
   the Angular 21 + PrimeNG 21 baseline; lazy chunks: admin-analytics 4.42 kB, governance-queue 4.31 kB,
  configuration 540 B, business-rules 536 B, schemas 527 B)
- [x] Chat page matches the agreed AdminLTE-style layout (thread, right rail, composer) with the left contacts column absent
- [x] Agent activity streams live over SignalR; the composer allows at most one clarification question per turn (BRD-NFR-07)
- [x] Existing governance toggle and analytics route keep working against unchanged APIs
- [x] Responsive behavior verified at `sm`, `md`, `lg`; thread has `aria-live`; keyboard-only operation works
  (sidebar collapses under `lg`, right rail stacks under `md` per `workspace.component.scss:40-54`;
  `chat-thread.component.html:2` has `aria-live="polite"`)
- [x] `npm test` passes and `ng build --configuration production` succeeds

---

## Out of scope (platform, per BRD §14)

- Backend API or persistence changes (Sprints 10–12)
- Global search / contacts list from the AdminLTE reference
- Real-time multi-user chat between people (this is a human-to-agent chat)
- Direct client access to MongoDB or NVIDIA

---

## Exit artifacts

- `src/web/src/app/app.component.*` shell, sidebar, and topbar
- `src/web/src/app/workspace/*` chat page (thread, right rail, composer, agent-activity strip)
- `src/web/src/app/app.config.ts` PrimeNG providers; `app.routes.ts` lazy routes
- Placeholder pages for Schemas, Configuration, Business Rules
- Updated component/spec files and a short Sprint 9 UI notes section in the PR description
