# Executive Showcase — Interactive Workflow Design

**Date:** 2026-09-16
**Status:** Draft for review
**Owner:** Bikash Ranjan Nayak
**Deliverable:** `showcase/index.html` (single, self-contained file)

---

## 1. Goal

Give senior managers and executives a clear, non-technical way to understand what
this platform does, what already works today, and where it is going. The showcase
must be presentable live (projector), shareable as one file, and honest about status.

## 2. Audience and success criteria

- **Primary audience:** senior managers and higher-ups (non-engineers).
- **Success looks like:** within 60 seconds they understand the problem and the
  promise; within 5 minutes they have seen one request travel end to end and know
  which capabilities are built versus planned.

## 3. Scope

**In scope**

- One guided interactive story: an executive summary screen plus a six-act
  walkthrough that follows a single realistic request through the platform.
- A clear status marker on every capability: `Built today` or `Coming next`.
- Plain-business language. No architecture jargon on the main path.

**Out of scope (this pass)**

- PPTX generation (approach documented in section 8; built later).
- Any backend or live data call. All behaviour is simulated in the browser.
- Replacing the working SPA or the POC demo.
- Reworking `brd.html` (the existing technical view remains separate; it may get a
  light accuracy pass later).

## 4. Story structure

### 4.1 Opening screen — Executive summary

- Problem statement in one sentence.
- Vision in one sentence.
- Six value pillars.
- A status badge per sprint (1–8).
- Primary call to action: **Walk me through it**.

### 4.2 Walkthrough — six acts (maps to eight sprints)

| Act | Title (plain language) | What the user sees | Business benefit | Sprint(s) | Status |
| --- | --- | --- | --- | --- | --- |
| 1 | Ask a question, safely signed in | Personalised, secure entry | Trusted access, no shared accounts | 1 | Built today |
| 2 | Instant answers to everyday questions | Answer in well under a second | No AI cost, no waiting, predictable | 2 | Built today |
| 3 | Hard questions answered by AI | AI drafts the query, then self-checks it | Handles complex asks; fewer analyst tickets | 3 | Built today |
| 4 | Sensitive data pauses for approval | Query halts; approver is asked | Compliance by design; no silent exposure | 4–5 | Coming next |
| 5 | Approved query runs, everything is audited | Results returned; an audit record is written | Traceability and accountability | 6 | Coming next |
| 6 | Insights, exports, and always-on delivery | Briefings, exports, dashboards, releases | Decision support; dependable operation | 7–8 | Coming next |

### 4.3 Closing screen

- One-line value recap.
- Roadmap strip (built versus planned).
- What we need next (decision/ask).

## 5. Interaction model

- **Navigation:** `Next` / `Back` buttons, a six-step progress rail, and keyboard
  arrows plus space. `Esc` returns to the summary.
- **Scenario chips:** at the start of the walkthrough the presenter picks
  `Simple question`, `Complex question`, or `Sensitive question`; the walkthrough
  branches at acts 2–4 accordingly.
- **Step animation:** each act reveals in short beats (label, then detail), with a
  single moving indicator along a simple horizontal journey line. Beats are
  skippable by clicking `Next` again.
- **Reduced motion:** when the operating system requests reduced motion, reveals
  are instant and the indicator does not animate.
- **Status pills:** every act carries a `Built today` or `Coming next` pill.

## 6. Visual design

- **Theme:** light executive. Neutral background, dark slate text, one accent
  colour (deep blue) plus a green (built) and amber (coming) status colour.
- **Typography:** system font stack (no web fonts) so it works offline; large
  sizes suitable for projection.
- **Iconography:** simple inline SVG shapes only. **No emojis anywhere.**
- **Layout:** generous whitespace; target 1280x720 and above; responsive down to
  roughly 1024px wide; readable projected and on laptop screens.
- **Accessibility:** keyboard operable, visible focus states, colour contrast meets
  WCAG AA for text, and `prefers-reduced-motion` is respected.

## 7. Technical approach

- **One file:** `showcase/index.html` with inline CSS and JavaScript. No external
  scripts, fonts, images, or network calls. Opens by double-click and works offline.
- **Content as data:** all slide/act copy lives in a single `const DECK = { ... }`
  block at the top of the file, separate from rendering code. This keeps the file
  the single source of truth and lets a future deck generator read the same content.
- **Rendering:** a small, dependency-free script renders the summary, acts, rail,
  and pills from `DECK`. State is derived from `actIndex` plus the chosen scenario.
- **Sample data:** realistic but clearly fabricated listing values (name, market,
  price, rating). No personal or sensitive data.
- **Accuracy guardrails:** correct product names only (ASP.NET Core 10, Angular 21,
  Semantic Kernel, NVIDIA NIM, MongoDB). No future capability is shown as working.
  No `[cite: ...]` artifacts and no unresolved placeholders.

## 8. Future PPT generation (documented, not built this pass)

Because content lives in `DECK`, the deck can be produced without rewriting copy:

1. **Preferred:** a generator script reads `DECK` and emits `.pptx` via
   `python-pptx`, one slide per act (title, user experience, benefit, status pill).
2. **Alternative:** mirror `DECK` into Markdown and use `Marp` to export
   `.pptx` / `.pdf`.
3. The HTML itself can be printed to PDF for handouts.

The chosen route is confirmed when the deck is requested.

## 9. Acceptance criteria

- Opens offline by double-click with no console errors and no network requests.
- Summary screen shows a status badge for all eight sprints.
- Walkthrough advances and reverses via buttons and keyboard; the progress rail
  reflects the current act.
- Each act shows the plain-language user experience, the business benefit, and the
  correct status pill.
- No emojis, no `[cite: ...]`, no placeholder text.
- Legible and usable at 1280x720 and 1024px width; reduced-motion honoured.

## 10. Non-goals and risks

- **Not a live product demo.** It is a walkthrough; the working SPA/POC remains the
  live demo. This is stated on the opening screen.
- **Overclaiming risk.** Mitigated by the `Built today` / `Coming next` pill on
  every act and correct product naming.
- **Content drift risk.** Mitigated by keeping copy in `DECK` so the HTML and the
  future deck share one source.

## 11. File location

- `showcase/index.html` at the repository root, so it is easy to open locally and
  straightforward to publish as a static preview.
