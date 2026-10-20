# Specification: Sakai-ng-Based UI Redesign

**Date:** 2026-10-20  
**Status:** Design Draft (Pending Review)  
**Target Sprints:** Sprint 14  
**Branch:** `261020-feat-ui-design-enhancement`  
**Author:** Multi-Agent Coding Assistant

---

## 1. Overview & Objectives

Redesign the existing Angular SPA (`src/web`) as a Sakai-ng-inspired workspace shell. The goal is a full-featured portal layout with Sakai v21.0.0 layout primitives, theme and configurator support, Tailwind 4 utilities, responsive behavior, localStorage persistence, and a user role chip in the topbar. All existing backend contracts, routes, and business behavior remain unchanged; only presentation and navigation are redesigned.

### Objectives

- Port Sakai layout primitives (`AppLayout`, `AppTopbar`, `AppSidebar`, `AppMenu`, `AppMenuItem`, `AppFooter`, `AppConfigurator`) adapted to our codebase conventions.
- Apply Sakai Aura theme tokens, adapted to Tailwind 4, with light/dark mode via `.app-dark`.
- Use **Lato** as the overall font family with system fallback.
- Persist layout preferences (dark mode, sidebar state, configurator presets) in `localStorage`.
- Show a **role chip** in the topbar instead of a profile menu.
- Restyle all 7 existing screens under one flat "Workspace" menu group; keep routes flat.
- Preserve Angular 21 + PrimeNG 21 + zone.js (not zoneless).
- Keep console-error-free Angular production builds (`ng test`, `ng build`, deploy preview).
- Do not modify `docs/BRD_V1.md`; leave historical SSE mentions untouched.

### Non-Goals

- Backend API changes, endpoint rewrites, or contract changes.
- User authentication/authorization changes; role is read from existing session state.
- Rebuilding the app from the Sakai starter template or replacing all screens with demo pages.
- Adding Angular signals-based zoneless architecture or SSR.
- Migrating to SCSS-only reskin without layout shell.

---

## 2. Assumptions & Locked Decisions

- Sakai-ng v21.0.0 MIT licensed clone exists at `/tmp/opencode/sakai-ng` with `src/assets` submodule initialized.
- Existing app runs Angular 21.2, PrimeNG 21.1.10, Tailwind 4, zone.js, Vitest, `@angular/build:application`.
- Existing 58 frontend tests must continue to pass; no behavioral regression.
- Current branch `261020-feat-ui-design-enhancement` is clean and pushed to `origin`.
- User-approved minor choices: **Lato overall**, **localStorage persistence**, **role chip for now**.
- Topbar user role chip reads existing `SessionService.role()` (or equivalent) and shows `Data Owner / Admin`, `Analyst`, or `Guest`; no profile menu for now.
- Configurator persists presets/surfaces/dark mode to `localStorage` under key `multi-agent-nlp.layout`.
- Font: Lato via Google Fonts preconnect link in `index.html` with system-ui fallback.
- Layout source files live under `src/styles/layout/` (not `src/assets/`) so Angular does not emit raw SCSS to `assets/`.
- No `additionalProperties: false` JSON schema concerns apply to this UI task.

---

## 3. Architecture & Components

### 3.1 Shell

`AppComponent` becomes the layout host and renders `<app-layout>`. `AppLayout` renders:

- `AppTopbar` — fixed topbar, app logo/title, spacer, user role chip.
- `AppSidebar` — slide-out drawer on mobile, persistent on desktop.
- `AppMenu` — flat route list under one "Workspace" group; active route highlighting.
- Router outlet / page body — flex column so chat and cards can use `height: 100%`.
- `AppFooter` — footer text, version link.
- `AppConfigurator` — floating theme/color/dark-mode/sidebar controls.

### 3.2 Services

- `LayoutService` — singleton UI state: `sidebarVisible`, `darkMode`, `layoutPreset`, `surface`, `menuMode`. Initialized from `localStorage` with browser guards. Writes changes back to `localStorage`. Exposes signal-like observables or behavior subjects consumed by shell components.
- `ThemeService` — applies `.app-dark`/`document.documentElement.classList` toggles and PrimeNG `darkModeSelector: '.app-dark'`.
- Existing `SessionService` supplies `role()` for the chip; no new auth.

### 3.3 Layout Components

Create under `src/app/layout/component/`:

- `app-layout.component.ts/html/scss`
- `app-topbar.component.ts/html/scss`
- `app-sidebar.component.ts/html/scss`
- `app-menu.component.ts/html/scss`
- `app-menuitem.component.ts/html/scss`
- `app-footer.component.ts/html/scss`
- `app-configurator.component.ts/html/scss`

Add `src/app/layout/service/layout.service.ts`.

### 3.4 Shared Page Primitives

Avoid duplicating layout scaffolding on every page. Add global CSS classes under `src/styles/layout/_app.scss` (and/or a small shared `PageHeaderComponent` if needed):

- `.page` — page wrapper, `padding` responsive.
- `.page-header` — title + subtitle + actions row.
- `.page-title` / `.page-subtitle`.
- `.page-actions` — action buttons aligned right.
- `.card` / `.card-header` / `.card-body` / `.card-footer`.
- `.table-card` — scrollable table wrapper with sticky header option.
- `.chip` / `.tag` / `.badge` for statuses.
- `.form-grid` — responsive two/three column form layout.
- `.footer-sticky` — sticky action bar for long tables/dialogs.

Keep existing page component templates where possible; wrap them in these classes rather than rewriting business logic.

---

## 4. Detailed Design

### 4.1 Routes & Menu

Keep `src/web/src/app/app.routes.ts` flat (7 routes, no feature folders). Menu model maps each route to a label/icon:

| Route | Label | Icon |
| --- | --- | --- |
| `/workspace/chat` | Chat | pi pi-comments |
| `/workspace/results` | Results | pi pi-chart-bar |
| `/workspace/governance` | Governance | pi pi-shield |
| `/workspace/admin` | Admin | pi pi-cog |
| `/workspace/schemas` | Schemas | pi pi-database |
| `/workspace/business-rules` | Business Rules | pi pi-file-edit |
| `/workspace/configuration` | Configuration | pi pi-sliders-h |
| `/workspace/mcp-servers` | MCP Servers | pi pi-server |

One menu group labeled **Workspace** containing all seven items. Active item follows router link exact matching.

### 4.2 Screen-by-Screen Restyling

**Workspace / Chat**  
- Replace fixed `calc(100vh - 108px)` heights with flex `height: 100%` inside layout main.
- Chat thread and input inside `.card` or full-bleed panel; two-column grid on desktop (chat flex 1, case context 320px), stacked on mobile `< 992px`.
- Preserve existing message threading, typing, and SignalR behavior.

**Results**  
- Page header with title/subtitle and export/refresh actions.
- Results table inside `.table-card`; status badges and typography tokens.
- Keep existing table columns and pagination.

**Governance**  
- Page header with title/subtitle and filter actions.
- Summary chips row: pending / approved / rejected counts computed from existing requests.
- Request cards with status tags and action buttons in `.card`.
- Sticky action bar for long lists if needed.

**Admin**  
- Page header with last refresh time.
- KPI cards row (6 cards): requests, users, agents, schemas, rules, MCP servers.
- Breakdown cards (agent states, rule types, etc.) in responsive grid.
- Keep existing charts/tables restyled with PrimeNG theme tokens.

**Schemas**  
- Page header with Import Schema / Create Schema actions.
- Schema table inside `.table-card`; active/inactive status chips.
- Import/create/edit dialogs keep existing structure; responsive width (`max-width: 90vw`).

**Business Rules**  
- Page header with Create Rule action.
- Segmented tabs (All / Active / Inactive) using PrimeNG tabs or segmented buttons.
- Rule table inside `.table-card`; condition previews and status tags.
- Rule editor dialog restyled with `.form-grid`.

**Configuration**  
- Page header with Save action footer.
- Tabs for settings groups; settings rows in `.form-grid`; switches and inputs restyled.
- Sticky Save/Cancel footer.

**MCP Servers**  
- Page header with Add MCP Server action.
- Server table inside `.table-card`; status tags and capability badges.
- Add/edit dialogs restyled; JSON/text inputs monospace.

### 4.3 Theming

- PrimeNG `providePrimeNG({ theme: { preset: Aura, options: { darkModeSelector: '.app-dark' } } })`.
- Remove `darkModeSelector: 'system'` and any zoneless flags.
- Tailwind 4 config in `tailwind.config.js` (or `tailwind.config.ts`) extends Sakai Aura token surface names (surface-0 through surface-9, text-color, primary, etc.) as CSS variables under `:root` and `.app-dark`.
- `src/styles/layout/_variables.scss` maps Sakai surface colors to Tailwind/theme variables.
- `src/styles/layout/_layout.scss` imports component SCSS in Sakai order: variables, layout, topbar, sidebar, menu, menuitem, footer, configurator, app.
- `src/styles.scss` imports `./styles/layout/layout` plus global utilities; `angular.json` styles entry includes `src/styles.scss` and `src/tailwind.css`.
- Lato imported via `<link>` preconnect in `index.html`; `font-family: 'Lato', system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif`; apply to `body` and PrimeNG components via CSS variables where needed.

### 4.4 localStorage Persistence

Key: `multi-agent-nlp.layout`. Shape:

```json
{
  "darkMode": false,
  "sidebarVisible": true,
  "layoutPreset": "sagga",
  "surface": "light"
}
```

- `LayoutService` reads on construction inside `isPlatformBrowser(this.platformId, 'browser')` guard.
- Changes write back synchronously to `localStorage`.
- On load, `ThemeService` applies `.app-dark` before first paint where possible; inline bootstrapping snippet in `index.html` reads `localStorage` and adds class to reduce flash.
- Stale/invalid JSON is ignored; defaults applied.

### 4.5 Accessibility & Responsive

- Sidebar toggles via hamburger; keyboard accessible; `aria-expanded`/`aria-controls`.
- Role chip has `aria-label="User role: <role>"`.
- Menu active states communicate `aria-current="page"`.
- Breakpoints: mobile `< 992px` sidebar drawer; desktop sidebar persistent.
- Tables scroll horizontally on small screens; cards stack.
- Color contrast follows Sakai Aura light/dark tokens.

### 4.6 Build & Bundle Budgets

- Proof-of-concept Tailwind + SCSS build before full port.
- `angular.json` budgets: initial `2MB` warning / `4MB` error; `anyComponentStyle` may need raising to `15kB` warning / `20kB` error if layout CSS grows; component CSS budgets remain.
- No `additionalProperties: false` concerns.

---

## 5. File Plan

### New

- `src/styles/layout/_variables.scss`
- `src/styles/layout/_layout.scss`
- `src/styles/layout/_topbar.scss`
- `src/styles/layout/_sidebar.scss`
- `src/styles/layout/_menu.scss`
- `src/styles/layout/_menuitem.scss`
- `src/styles/layout/_footer.scss`
- `src/styles/layout/_configurator.scss`
- `src/styles/layout/_app.scss`
- `src/styles.scss` (updated imports)
- `src/tailwind.css` (unchanged or extended Tailwind directives)
- `src/app/layout/service/layout.service.ts`
- `src/app/layout/component/app-layout.component.ts/html/scss`
- `src/app/layout/component/app-topbar.component.ts/html/scss`
- `src/app/layout/component/app-sidebar.component.ts/html/scss`
- `src/app/layout/component/app-menu.component.ts/html/scss`
- `src/app/layout/component/app-menuitem.component.ts/html/scss`
- `src/app/layout/component/app-footer.component.ts/html/scss`
- `src/app/layout/component/app-configurator.component.ts/html/scss`
- `src/app/shared/page-header.component.ts/html/scss` (optional shared primitive)
- `tests/Gateway.Tests/UI/LayoutServiceTests.ts` (or equivalent frontend test location)

### Modified

- `src/web/src/app/app.component.ts/html/scss` — layout host wrapper
- `src/web/src/app/app.routes.ts` — confirm flat routes unchanged
- `src/web/src/app/main.ts` or `app.config.ts` — `providePrimeNG` theme options; `LayoutService` provided in root
- `src/web/src/index.html` — Lato preconnect link, inline dark-mode bootstrap
- `angular.json` — styles entries and budgets if needed
- Each of the 7 screen components — wrap in page/card classes; remove fixed viewport heights where conflicting

### Not Modified

- `docs/BRD_V1.md`
- Backend contracts, endpoints, hubs, services
- Existing test assertions unrelated to CSS classes

---

## 6. Testing

- Existing 58 tests must pass unchanged.
- Add `LayoutService` unit tests: defaults, localStorage read/write, dark mode toggle, sidebar toggle, preset/surface changes, invalid JSON fallback, browser guard.
- Add shell menu test: 7 items, one group, route mapping, active matching.
- Add `AppComponent` smoke test: renders `app-layout`, topbar role chip present, sidebar toggle present.
- Visual QA at 375px, 768px, 1024px, 1440px for all 7 screens.
- `ng build` production and `ng test --watch=false` clean.
- Deploy preview via `/deploy-website` after build.

---

## 7. Risks & Mitigations

| Risk | Mitigation |
| --- | --- |
| Tailwind 4 + Angular build integration | PoC build before port; keep `tailwind.css` entry separate |
| Sakai API drift vs PrimeNG 21.1.10 | Build after each component port; adapt imports/APIs |
| Layout CSS published to `assets/` | Keep SCSS under `src/styles/`, not `src/assets/` |
| Dark-mode flash on load | Inline `localStorage` bootstrap in `index.html` |
| Chat height regression | Flex `height: 100%`; QA at breakpoints |
| Configurator complexity | Isolate configurable surfaces; test persistence separately |
| localStorage stale values | Validate/sanitize on read; apply defaults on invalid |

---

## 8. Acceptance Criteria

- [ ] Shell renders `AppTopbar`, `AppSidebar`, `AppMenu`, `AppFooter`, `AppConfigurator`, router outlet.
- [ ] One flat "Workspace" menu group with all 7 routes; active route highlighted.
- [ ] Lato font applied overall with system fallback.
- [ ] Dark mode toggle persists in `localStorage` and applies `.app-dark`.
- [ ] Role chip visible in topbar; no profile menu.
- [ ] All 7 screens restyled with page/card/table primitives; no fixed `calc(100vh - ...)` chat heights.
- [ ] Responsive sidebar drawer below 992px; persistent above.
- [ ] Existing 58 tests pass; new `LayoutService` + menu tests pass.
- [ ] `ng build` production clean within budgets; no console errors.
- [ ] Deploy preview loads and navigates all routes.
- [ ] No changes to `docs/BRD_V1.md` or backend contracts.

---

## 9. Rollout

1. Review and approve this spec.
2. Create PoC branch work on `261020-feat-ui-design-enhancement`.
3. Implement Tailwind toolchain + theme tokens first; verify `ng build`.
4. Port layout shell components one at a time; verify build after each.
5. Restyle 7 screens; verify tests/build at each screen.
6. Add `LayoutService` tests and localStorage persistence.
7. QA responsive breakpoints; fix chat height and table scroll.
8. Run `ng test --watch=false` and `ng build`; deploy preview.
9. User sign-off; then proceed to MongoDB index/validator work on next branch.
