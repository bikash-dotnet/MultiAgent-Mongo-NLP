---
name: test-driven-development
description: Use when implementing any feature or bugfix, before writing implementation code. Write failing tests first, then implement to make them pass, then refactor.
---

# Test Driven Development

Use this skill when implementing any feature or bugfix, before writing implementation code.

## Process

1. **Write a failing test** that defines the desired behavior
2. **Run the tests** to confirm the new test fails
3. **Write the minimal implementation** to make the test pass
4. **Run the tests** to confirm all pass
5. **Refactor** the implementation while keeping tests green
6. **Repeat** for the next behavior

## For This Project

- **.NET Gateway tests**: `tests/Gateway.Tests/Gateway.Tests.csproj` — run with `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj`
- **Angular SPA tests**: `src/web` — run with `npm test` (vitest, `--watch=false`)

## CI

The GitHub Actions workflow (`.github/workflows/build.yml`) runs `dotnet test` and `npm test` on push/PR to `main`.

## Principles

- Test edge cases: null, zero, negative, empty lists, single element, concurrent access
- Keep tests focused on correct logic, not just "making it run"
- Do not write tests merely to "make them run" — they must verify correct behavior
- Update tests when behavior changes; never delete tests to hide regressions
