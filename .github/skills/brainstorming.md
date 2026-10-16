---
name: brainstorming
description: Use before any creative work — creating features, building components, adding functionality, or modifying behavior. Explores user intent, requirements and design before implementation.
---

# Brainstorming Ideas Into Designs

Help turn ideas into fully formed designs and specs through natural collaborative dialogue. Use this **before** any creative work.

## Hard Gate

Do NOT invoke any implementation skill, write any code, scaffold any project, or take any implementation action until you have presented a design and the user has approved it. This applies to EVERY project regardless of perceived simplicity.

## Checklist

Complete these in order:

1. **Explore project context** — check files, docs, recent commits
2. **Offer the visual companion just-in-time** — only when a question is genuinely clearer shown than described
3. **Ask clarifying questions** — one at a time, understand purpose/constraints/success criteria
4. **Propose 2-3 approaches** — with trade-offs and your recommendation
5. **Present design** — in sections scaled to complexity, get user approval after each section
6. **Write design doc** — save to `docs/superpowers/specs/YYYY-MM-DD-<topic>-design.md`
7. **Spec self-review** — check for placeholders, contradictions, ambiguity, scope
8. **User reviews written spec** — ask user to review before proceeding
9. **Transition to implementation** — invoke `writing-plans` skill

## For This Project

Design docs live in `docs/superpowers/specs/` and plans in `docs/superpowers/plans/`. Follow existing conventions when adding new specs (e.g. `YYYY-MM-DD-<topic>-design.md`).

## Key Principles

- One question at a time
- Multiple choice preferred when possible
- YAGNI ruthlessly — remove unnecessary features
- Always propose 2-3 approaches before settling
- Incremental validation — present design, get approval before moving on

## After the Design

- Invoke `writing-plans` to create the implementation plan
- Do NOT invoke any other implementation skill after brainstorming
