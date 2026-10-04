# ADR-0001 — UI component framework for the Angular 21 SPA

- **Status:** Accepted
- **Date:** 2026-10-04
- **Deciders:** Platform team
- **Related:** BRD v4.2 §3.1 (Frontend), Sprint 9 (`sprints/sprint-9.md`)

---

## Context

The SPA (`src/web`) is Angular 21 with standalone components, RxJS, and a single
`provideHttpClient(withInterceptors(...))` bootstrap. It currently ships hand-rolled HTML/SCSS
with no component library. Sprints 9–12 add an app shell, an AdminLTE-style chat experience,
schema editors, a configuration console, and business-rule / column-definition builders. Those
screens need data grids, trees, nested forms, dialogs, toggles, and input controls that are
expensive and risky to hand-build.

The team must standardize on one component framework before Sprint 9 starts. The candidates
requested were **PrimeNG**, **NG-ZORRO (ng-zorro-antd)**, and **AdminLTE**. AdminLTE was
referenced specifically for the chat layout at `https://adminlte.io/themes/v4/pages/chat.html`
(search/contacts column on the left is explicitly out of scope).

---

## Decision

Adopt **PrimeNG** (with the **Aura** design-token preset via `@primeuix/themes`) as the single
UI component framework for the SPA. Use the AdminLTE chat page **only as a visual/interaction
reference**; it is not a dependency.

Adoption is constrained by the rules in Sprint 9:

- Components are imported per standalone component, not globally, so the build stays tree-shakable.
- Feature routes (`/schemas`, `/configuration`, `/business-rules`) are lazy-loaded.
- PrimeNG's design tokens drive theming; incidental SCSS is minimized.
- Existing `EventSource`/`conversation`/`nlp-query` services remain the data layer; the framework
  is presentation-only.

---

## Options considered

### 1. PrimeNG (chosen)

**Advantages**

- Angular-native, released in lockstep with Angular majors; standalone-component templates and
  reactive forms integrate without wrappers.
- Broadest enterprise set in scope: `p-table` (virtual scroll, filtering, row expansion, column
  reorder/resize/export), `p-tree`/`p-treetable`, `p-dialog`/`p-dynamicDialog`, `p-tabs`,
  `p-inputNumber`, `p-inputSwitch`, `p-dropdown`/`p-select`, `p-chips`, `p-toast`, `p-confirmDialog`.
- First-class support for the exact screens planned: schema trees, config forms, rule builders,
  result grids, chat bubbles.
- Design-token theming (Aura/Material/Lara) and a scoped Theme Designer; dark mode via tokens.
- MIT licensed, active maintenance, large community, accessible (WCAG-oriented) components.

**Disadvantages**

- Bundle growth if components are eagerly imported; the Spring Boot budgets in `angular.json`
  (`500 kB` warning / `1 MB` error initial) must be revisited with lazy loading.
- Opinionated theming; heavy visual redesigns require token overrides rather than plain CSS.
- Frequent major releases mean periodic upgrade work to stay aligned with Angular.
- Some advanced components carry a learning curve and their own docs conventions.

### 2. NG-ZORRO (ng-zorro-antd)

**Advantages**

- Angular-native, OnPush-friendly, clean Ant Design visual language.
- Solid forms, `nz-table`, `nz-tree`, `nz-descriptions`, i18n, MIT licensed.
- Lighter configuration than PrimeNG and a calmer upgrade cadence historically.

**Disadvantages**

- Weaker fit for the most demanding screens: no editable data grid with the same richness as
  PrimeNG's `p-table` (grouping, virtual scroll, column reorder/resize/export), and less mature
  chat/timeline/organization visuals.
- Adopting the Ant Design identity across the shell would require substantial overrides to match
  the governance/admin look.
- Smaller ecosystem for enterprise data-editing patterns (rule builders, schema trees).

NG-ZORRO is a reasonable fallback if PrimeNG's theming footprint proves unacceptable.

### 3. AdminLTE

**Advantages**

- Matches the referenced chat page out of the box; familiar bootstrap admin aesthetic.
- Many ready-made layout blocks and dashboard widgets.

**Disadvantages**

- Not Angular-native: it is a Bootstrap/jQuery template. Integrating it into an Angular 21
  standalone app requires manual wrappers, dangling jQuery globals, and duplicate layout systems.
- No Angular DI, components, typing, or reactive-form integration; hard to unit test and to
  maintain alongside Angular upgrades.
- jQuery global state conflicts with Angular change detection and Angular CDK overlays.
- The Angular community and Angular docs consistently advise against mixing jQuery plugins into
  Angular apps.

AdminLTE is rejected as a dependency. Its chat layout is reproduced with PrimeNG components.

---

## Consequences

**Positive**

- One consistent component vocabulary across Sprints 9–12, faster delivery of editors and grids.
- Theming and dark mode come from design tokens rather than bespoke SCSS.
- Accessibility and keyboard behavior are inherited rather than re-implemented.

**Negative / mitigations**

- Initial bundle growth -> import per component, lazy-load feature routes, and set budgets in
  Sprint 9-01 rather than raising the error threshold.
- Version drift -> pin the PrimeNG major to the Angular major and upgrade deliberately in a
  dedicated chore, not mid-feature.
- Theming lock-in -> keep a thin presentation boundary (components depend on our view models, not
  on PrimeNG types) so a future swap to NG-ZORRO is a UI-layer change.

---

## Migration plan (executed in Sprint 9)

1. Add `primeng` and `@primeuix/themes`; register the Aura preset and the overlay/Message/Confirm
   services in `app.config.ts`.
2. Build the app shell (sidebar + topbar) with `p-panelMenu`/`p-menubar` and `p-avatar`.
3. Rebuild the workspace as a chat page with `p-scrollPanel`, message templates, and a composer.
4. Re-style `/governance` and `/admin` onto PrimeNG `p-table`/`p-card` without changing their
   service contracts.
5. Add the `/schemas`, `/configuration`, and `/business-rules` lazy routes in Sprints 10–12.
6. Verify `npm test`, `ng build --configuration production`, and the dev-server `allowedHosts`
   proxy still pass.

---

## References

- BRD v4.2 §3.1 Frontend, §9 UX requirements
- `sprints/sprint-9.md` … `sprints/sprint-12.md`
- primefaces/primeng (MIT), NG-ZORRO/ng-zorro-antd (MIT), ColorlibHQ/AdminLTE (MIT)
