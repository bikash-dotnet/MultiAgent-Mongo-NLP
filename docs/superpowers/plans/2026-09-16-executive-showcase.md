# Executive Showcase Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a single self-contained, executive-facing interactive showcase at `showcase/index.html` that tells the platform story through an executive summary plus a six-act guided walkthrough.

**Architecture:** One HTML file with inline CSS and JavaScript and no external assets or network calls. All copy lives in a `const DECK = { ... }` data block at the top; a small dependency-free renderer draws the summary screen, the six acts, the progress rail, and the status pills from that data. Interactivity is client-side state (`screen`, `actIndex`, `scenario`) plus CSS reveal animations that honour `prefers-reduced-motion`.

**Tech Stack:** Plain HTML5, CSS, and vanilla JavaScript (ES2017). No build step, no dependencies, no frameworks.

## Global Constraints

- Single file: `showcase/index.html` only. No external CSS/JS/fonts/images, no network requests.
- No emojis anywhere in the file.
- No `[cite: ...]` artifacts, no `TODO`/`TBD`, no unresolved `{{placeholders}}`.
- Correct product names only: ASP.NET Core 10, Angular 21, Semantic Kernel, NVIDIA NIM, MongoDB.
- Every act carries a `Built today` or `Coming next` status pill. No future capability is shown as working.
- Copy lives in `DECK`; rendering code reads `DECK` and must not hardcode slide text.
- Light executive theme; system font stack; WCAG AA text contrast; `prefers-reduced-motion` honoured.

---

### Task 1: Content model and executive summary screen

**Files:**
- Create: `showcase/index.html`

**Interfaces:**
- Consumes: nothing (first task).
- Produces: `DECK` object with shape
  `{ meta, summary: { problem, vision, pillars[], }, sprintStatus: [{ n, name, status }], acts: [{ id, title, userSees, benefit, status, sprints }] }`;
  CSS custom properties `--bg`, `--surface`, `--text`, `--muted`, `--accent`, `--built`, `--coming`;
  DOM anchors `#summary`, `#walkthrough`, `#rail`.

- [ ] **Step 1: Create the file with the content block and summary screen**

Create `showcase/index.html`. Include the `DECK` block with the summary content and all six acts from the spec (section 4.2), the eight `sprintStatus` entries, the light-theme CSS variables, the summary section markup, and a renderer that populates the summary and the eight sprint badges and wires the **Walk me through it** button to `show('walkthrough')`.

Key structure:

```html
<script>
const DECK = {
  meta: { title: 'Multi-Agent MongoDB NLP', subtitle: 'Natural-language answers with governance built in' },
  summary: {
    problem: 'Business users wait on analysts and engineers for answers locked in data they cannot query directly.',
    vision: 'Ask in plain English. Get governed, auditable answers in seconds.',
    pillars: ['Ask naturally', 'Instant simple answers', 'AI for hard questions', 'Governance by design', 'Traceable outcomes', 'Insight on demand']
  },
  sprintStatus: [ /* n, name, status for sprints 1..8 */ ],
  acts: [ /* six acts: id, title, userSees, benefit, status, sprints */ ]
};
</script>
```

- [ ] **Step 2: Verify the summary renders**

Run: `node -e "const h=require('fs').readFileSync('showcase/index.html','utf8'); if(!/DECK/.test(h)||!/Walk me through it/.test(h)) process.exit(1); console.log('summary markers ok')"`
Expected: `summary markers ok`

- [ ] **Step 3: Commit**

```bash
git add showcase/index.html
git commit -m "Add executive showcase summary screen and content model"
```

---

### Task 2: Six-act walkthrough, rail, and interactions

**Files:**
- Modify: `showcase/index.html`

**Interfaces:**
- Consumes: `DECK` from Task 1.
- Produces: `show(screen)`; `go(delta)`; `renderAct()`; `setScenario(id)`; keyboard handlers for ArrowLeft/ArrowRight/Space/Escape; elements `#rail`, `#actTitle`, `#actUserSees`, `#actBenefit`, `#statusPill`.

- [ ] **Step 1: Add the walkthrough markup and renderer**

Add the walkthrough section with the progress rail, the single horizontal journey line with one moving indicator, the act card (title, `userSees`, `benefit`, status pill), `Next`/`Back` buttons, and the scenario chips (`Simple question`, `Complex question`, `Sensitive question`). Render every act from `DECK.acts`, from `actIndex`, and branch the act copy at acts 2–4 by `scenario`.

- [ ] **Step 2: Add interactions and accessibility**

Wire `Next`/`Back` buttons, the rail steps, and keyboard (ArrowRight/Space forward, ArrowLeft back, Escape to summary). Add visible focus states, `aria-live` on the act card, and a `@media (prefers-reduced-motion: reduce)` block that disables transitions and reveal animation.

- [ ] **Step 3: Verify structure and interaction hooks**

Run: `node -e "const h=require('fs').readFileSync('showcase/index.html','utf8'); const need=['#rail','prefers-reduced-motion','ArrowRight','setScenario','renderAct']; const miss=need.filter(x=>!h.includes(x)); if(miss.length){console.error('missing',miss);process.exit(1)} console.log('interaction hooks ok')"`
Expected: `interaction hooks ok`

- [ ] **Step 4: Commit**

```bash
git add showcase/index.html
git commit -m "Add interactive six-act walkthrough to executive showcase"
```

---

### Task 3: Compliance checks and documentation

**Files:**
- Modify: `README.md`
- Test: `showcase/verify.mjs`

**Interfaces:**
- Consumes: `showcase/index.html`.
- Produces: `showcase/verify.mjs` that exits non-zero on emojis, `[cite`, `TODO`, unresolved `{{`, external `http(s)` asset references, or a missing status pill; and a README line describing the showcase.

- [ ] **Step 1: Add the verification script**

Create `showcase/verify.mjs`:

```js
import { readFileSync } from 'node:fs';
const html = readFileSync(new URL('./index.html', import.meta.url), 'utf8');
const emoji = /\p{Extended_Pictographic}/u;
const checks = [
  ['emoji', emoji.test(html)],
  ['cite', html.includes('[cite')],
  ['todo', /\b(TODO|TBD)\b/.test(html)],
  ['placeholder', /\{\{[^}]+\}\}/.test(html)],
  ['external-asset', /(?:src|href)\s*=\s*["']https?:/i.test(html)]
];
const failed = checks.filter(([, bad]) => bad).map(([name]) => name);
if (failed.length) { console.error('FAIL:', failed.join(', ')); process.exit(1); }
if (!html.includes('Built today') || !html.includes('Coming next')) { console.error('FAIL: status pills missing'); process.exit(1); }
console.log('showcase checks passed');
```

- [ ] **Step 2: Run the checks**

Run: `node showcase/verify.mjs`
Expected: `showcase checks passed`

- [ ] **Step 3: Document the showcase in the README**

Add a short section: what `showcase/index.html` is, that it opens offline by double-click, and that the content lives in `DECK` for future deck generation.

- [ ] **Step 4: Commit**

```bash
git add showcase/verify.mjs README.md
git commit -m "Add showcase verification script and README note"
```

---

## Self-Review

- **Spec coverage:** summary screen (Task 1), six acts and pills (Task 1 data, Task 2 render), interaction model including keyboard and reduced motion (Task 2), visual constraints and no-emoji rule (Global Constraints, Task 3 checks), future PPT path already documented in the spec (no build needed this pass), file location `showcase/index.html` (Task 1). No gaps.
- **Placeholder scan:** no `TODO`/`TBD`; the `DECK` skeleton in Task 1 is intentional and its exact contents are enumerated in the spec section 4.
- **Type consistency:** `DECK` field names (`summary`, `sprintStatus`, `acts`, `userSees`, `benefit`, `status`) are used consistently across Task 1, Task 2, and Task 3.
