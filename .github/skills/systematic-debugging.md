---
name: systematic-debugging
description: Use when encountering any bug, test failure, or unexpected behavior, before proposing fixes. Reproduce, isolate, identify root cause, fix, verify, and add regression tests.
---

# Systematic Debugging

Use this skill when encountering any bug, test failure, or unexpected behavior, before proposing fixes.

## Process

### 1. Reproduce

Consistently reproduce the issue. Capture exact steps, inputs, and expected vs. actual behavior.

### 2. Isolate

Narrow down the failing component. Use logs, breakpoints, and minimal reproductions.

### 3. Identify root cause

Determine the underlying cause, not just the symptom. Check recent changes, dependencies, and configuration.

### 4. Fix

Apply a minimal, targeted fix that addresses the root cause.

### 5. Verify

Re-run the reproduction case and the full test suite to confirm the fix and check for regressions.

### 6. Add regression tests

Add tests that would have caught the bug, so it cannot recur silently.

## For This Project

- .NET gateway issues: inspect `Program.cs`, middleware, NLP guardrail, governance, and execution modules
- Angular SPA issues: check `proxy.conf.json` routing (including WebSocket proxying), SignalR hub (`/hubs/agent-stream`), and vitest tests
- Test failures: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj` and `npm test` in `src/web`

## Principles

- Reproduce before fixing
- Fix the root cause, not the symptom
- Verify with the full test suite
- Add regression tests
- Document the fix in commits/PR description
