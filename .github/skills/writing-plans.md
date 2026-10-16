---
name: writing-plans
description: Use when you have a spec or requirements for a multi-step task, before touching code. Creates a structured implementation plan with ordered steps and verification checkpoints.
---

# Writing Plans

Use this skill when you have a spec or requirements for a multi-step task, before touching code.

## Process

1. **Review the spec/requirements** — understand scope, constraints, and acceptance criteria
2. **Break into ordered steps** — each step should be independently verifiable
3. **Identify dependencies** — sequence steps so later steps don't block earlier ones
4. **Add verification checkpoints** — define how to confirm each step works
5. **Surface risks and edge cases** — null, empty, concurrent, permission, timeout scenarios
6. **Present the plan** — get approval before implementing

## For This Project

The project follows sprints (`sprints/sprint-0.md` through `sprint-13.md`) and design specs in `docs/superpowers/specs/`. When planning work:

- Align with existing sprint cadence and design docs
- Reference relevant specs in `docs/superpowers/specs/` and plans in `docs/superpowers/plans/`
- Include verification steps matching CI (`dotnet test`, `npm test`, `npm run build`)
- Consider guardrails, governance, execution, audit, and persistence modules

## Output

A structured todo list (via `todowrite`) with:
- `content` — specific, actionable step
- `status` — `pending` / `in_progress` / `completed`
- `priority` — `high` / `medium` / `low`

Update status in real time; mark `completed` only after required work and verification are done.

## Principles

- One `in_progress` task at a time
- Preserve user-provided commands verbatim (flags, args, order)
- Break large work into smaller steps
- Verify before marking complete
