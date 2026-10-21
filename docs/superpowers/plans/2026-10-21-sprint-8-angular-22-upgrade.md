# Sprint 8: Angular 22 Upgrade & PrimeNG Tabs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade Angular from 21.2 to 22.x and PrimeNG from 21.1.10 to 22.x, then convert the business-rules button-based tabs to PrimeNG `p-tabView`/`p-tabPanel` using `TabsModule`.

**Architecture:** In-place dependency upgrade via `ng update`, peer dependency resolution (Angular CDK), build/test verification at each stage, then HTML component refactor replacing manual tab buttons with PrimeNG tab components.

**Tech Stack:** Angular 22.x, PrimeNG 22.x, Angular CDK 22.x, PrimeNG CDK peer, Tailwind 4, zone.js

## Global Constraints

- Angular version floor: `^22.0.0` — all `@angular/*` packages at 22.x
- PrimeNG version floor: `^22.0.0` — `primeng@22.x`
- Angular CDK peer: `@angular/cdk@^22.1.0` required by PrimeNG 22
- Do not upgrade Angular beyond 22.x unless explicitly requested
- Do not switch to zoneless — keep zone.js
- Conventional commits: `feat(web):`, `fix(web):`, `chore(web):`
- TDD: write failing test before implementation where applicable
- Frequent commits after each independently testable deliverable
- Build must be clean: `npx ng build --configuration production`
- Tests must pass: `npx ng test --watch=false` → 71 passing (baseline)
- Branch: create `261020-feat-angular-22-upgrade` from `main` after PR #26 merge

---

## Pre-flight: Verify PR #26 Merge State

- [ ] **Step: Confirm local `main` has PR #26 merged**

Run: `git log --oneline main -5`

Expected: `main` tip contains commit `57f1ac0` (remove duplicate Add rule button)

If missing: `git checkout main && git pull origin main`

- [ ] **Step: Verify working tree clean on `main`**

Run: `git status --short`

Expected: empty output

- [ ] **Step: Record baseline test count**

Run: `npx ng test --watch=false > /tmp/baseline-test.txt 2>&1; tail -3 /tmp/baseline-test.txt`

Expected: `71 passed`

---

### Task 1: Create upgrade branch

**Files:** none (git operation)

**Interfaces:** none

- [ ] **Step 1: Create branch `261020-feat-angular-22-upgrade` from `main`**

Run: `git checkout -b 261020-feat-angular-22-upgrade`

Expected: branch created, working tree clean

- [ ] **Step 2: Commit branch creation marker**

Run: `git commit --allow-empty -m "chore(web): create Angular 22 upgrade branch"` && `git push origin 261020-feat-angular-22-upgrade`

Expected: branch pushed to remote

---

### Task 2: Angular 22 dependency upgrade

**Files:**
- Modify: `src/web/package.json`

**Interfaces:** none

- [ ] **Step 1: Bump all `@angular/*` dependencies to `^22.0.0` in package.json**

Change:
```
"@angular/common": "^21.2.0",
"@angular/compiler": "^21.2.0",
"@angular/core": "^21.2.0",
"@angular/forms": "^21.2.22",
"@angular/platform-browser": "^21.2.0",
"@angular/router": "^21.2.0",
"@angular/build": "^21.2.23",
"@angular/cli": "^21.2.23",
"@angular/compiler-cli": "^21.2.0",
```
To:
```
"@angular/common": "^22.0.0",
"@angular/compiler": "^22.0.0",
"@angular/core": "^22.0.0",
"@angular/forms": "^22.0.0",
"@angular/platform-browser": "^22.0.0",
"@angular/router": "^22.0.0",
"@angular/build": "^22.0.0",
"@angular/cli": "^22.0.0",
"@angular/compiler-cli": "^22.0.0",
```

- [ ] **Step 2: Bump `zone.js` to `~0.16.0` (Angular 22 compatible)**

Change: `"zone.js": "~0.15.1"` → `"zone.js": "~0.16.0"`

- [ ] **Step 3: Install dependencies**

Run: `npm install`

Expected: no `ERESOLVE` errors; `@angular/*` at 22.x in `node_modules`

- [ ] **Step 4: Commit dependency changes**

Run: `git add package.json package-lock.json && git commit -m "chore(web): bump Angular to 22.x and zone.js to 0.16"` && `git push origin 261020-feat-angular-22-upgrade`

Expected: commit pushed

---

### Task 3: PrimeNG 22 upgrade + Angular CDK

**Files:**
- Modify: `src/web/package.json`

**Interfaces:** none

- [ ] **Step 1: Bump `primeng` to `^22.0.0`**

Change: `"primeng": "^21.1.10"` → `"primeng": "^22.0.0"`

- [ ] **Step 2: Install dependencies (Angular CDK peer will be pulled in)**

Run: `npm install`

Expected: `@angular/cdk@22.x` installed as peer of PrimeNG 22; no ERESOLVE errors

- [ ] **Step 3: Verify PrimeNG 22 exposes `TabsModule`**

Run: `ls node_modules/primeng/tabs/`

Expected: directory exists containing `tabview.d.ts`/`tabpanel.d.ts` or `tabs.d.ts`

- [ ] **Step 4: Commit PrimeNG upgrade**

Run: `git add package.json package-lock.json && git commit -m "chore(web): bump PrimeNG to 22.x with Angular CDK"` && `git push origin 261020-feat-angular-22-upgrade`

Expected: commit pushed

---

### Task 4: Build + test verification after dependency upgrade

**Files:** none

**Interfaces:** none

- [ ] **Step 1: Run production build**

Run: `npx ng build --configuration production`

Expected: `Application bundle generation complete.` — no errors

- [ ] **Step 2: Run test suite**

Run: `npx ng test --watch=false`

Expected: `71 passed` (or updated count if Angular 22 changed test behavior)

- [ ] **Step 3: Commit verification**

Run: `git commit --allow-empty -m "chore(web): verify build and tests pass after Angular 22 + PrimeNG 22 upgrade"` && `git push origin 261020-feat-angular-22-upgrade`

Expected: commit pushed

---

### Task 5: Business-rules component — convert button tabs to PrimeNG TabView

**Files:**
- Modify: `src/web/src/app/business-rules/business-rules.component.html`
- Modify: `src/web/src/app/business-rules/business-rules.component.ts`

**Interfaces:**
- Consumes: `TabsModule` import from `primeng/tabs`
- Produces: `p-tabView` / `p-tabPanel` structure replacing `tab-bar` div + button toggles

- [ ] **Step 1: Import `TabsModule` in business-rules module**

Edit `business-rules.component.ts` (or the module file that declares this component) to add `TabsModule` to imports.

Current imports to inspect — add:
```
import { TabsModule } from 'primeng/tabs';
```
and include `TabsModule` in the `@Component` or `NgModule` `imports` array.

- [ ] **Step 2: Replace button-based tabs with `p-tabView` / `p-tabPanel`**

Edit `business-rules.component.html`:
- Remove the `tab-bar` div with `p-button` toggles (Business Rules / Column Definitions buttons)
- Replace with:
```html
<p-tabView>
  <p-tabPanel header="Business Rules">
    <!-- existing Business Rules tab content -->
  </p-tabPanel>
  <p-tabPanel header="Column Definitions">
    <!-- existing Column Definitions tab content -->
  </p-tabPanel>
</p-tabView>
```
- Remove `selectedTab` state management from `.ts` if no longer needed
- Keep the single "Add rule" button inside the Business Rules tab panel only

- [ ] **Step 3: Verify build + tests**

Run: `npx ng build --configuration production && npx ng test --watch=false`

Expected: build clean; tests pass

- [ ] **Step 4: Commit tab conversion**

Run: `git add src/web/src/app/business-rules/business-rules.component.html src/web/src/app/business-rules/business-rules.component.ts && git commit -m "feat(web): convert business-rules button tabs to PrimeNG TabView"` && `git push origin 261020-feat-angular-22-upgrade`

Expected: commit pushed

---

### Task 6: End-to-end QA + merge

**Files:** none

**Interfaces:** none

- [ ] **Step 1: Deploy preview QA**

Start dev server on port 4200, verify:
- Business Rules page loads with PrimeNG tabs
- Tab switching works (Business Rules ↔ Column Definitions)
- "Add rule" button appears only once (inside Business Rules tab)
- No console errors

- [ ] **Step 2: Final build + test**

Run: `npx ng build --configuration production && npx ng test --watch=false`

Expected: build clean; all tests pass

- [ ] **Step 3: Open PR to merge `261020-feat-angular-22-upgrade` → `main`**

Expected: PR created, CI green, ready for review

---

## Discussion Points (Before Implementation)

1. **Angular 22 breaking changes** — Angular 22 may introduce breaking changes in forms, router, or compiler. We should review the Angular 22 migration guide before step 2.

2. **PrimeNG 22 `TabsModule` API** — Confirm exact component selectors (`p-tabView`, `p-tabPanel`) and event bindings differ from PrimeNG 21 patterns.

3. **Tailwind 4 compatibility with Angular 22** — Tailwind 4 is already in use; verify no conflicts with Angular 22's build pipeline.

4. **Zone.js version** — Angular 22 pairs with zone.js `~0.16.0`; confirm no zoneless migration is desired (we keep zone.js per constraints).

5. **Sprint scope** — Should this sprint include only the upgrade + business-rules tabs, or also apply `p-tabView` to other screens (governance-queue, admin-analytics, etc.)? Recommend: keep scope to business-rules only for this sprint; evaluate others in next sprint.

---

## Plan Review Checklist

- [ ] Spec coverage: Angular 21→22 upgrade covered (Tasks 2–4)
- [ ] Spec coverage: PrimeNG 21→22 + TabsModule covered (Task 3)
- [ ] Spec coverage: business-rules tab conversion covered (Task 5)
- [ ] Spec coverage: QA + merge covered (Task 6)
- [ ] No placeholders: all steps have exact commands/code
- [ ] Type consistency: `TabsModule` import path `primeng/tabs` consistent across tasks
- [ ] Baseline recorded: 71 tests passing before upgrade

---

**Plan complete and saved to `docs/superpowers/plans/2026-10-21-sprint-8-angular-22-upgrade.md`.**

**Two execution options:**

1. **Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration
2. **Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints for review

**Which approach?**
