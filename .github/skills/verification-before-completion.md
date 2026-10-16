---
name: verification-before-completion
description: Use when about to claim work is complete, fixed, or passing, before committing or creating PRs. Requires running verification commands and confirming output before making any success claims; evidence before assertions always.
---

# Verification Before Completion

Use this skill when about to claim work is complete, fixed, or passing, before committing or creating PRs. Requires running verification commands and confirming output before making any success claims. Evidence before assertions always.

## Process

### 1. Run verification commands

Execute the project's lint, typecheck, build, and test commands:

- .NET: `dotnet build src/gateway --no-restore --configuration Release`
- .NET tests: `dotnet test tests/Gateway.Tests/Gateway.Tests.csproj`
- Angular install: `npm ci` (in `src/web`)
- Angular tests: `npm test` (in `src/web`)
- Angular build: `npm run build -- --configuration production` (in `src/web`)

### 2. Confirm output

Read the actual command output. Confirm:
- Build succeeded with no errors
- All tests passed (note pass/fail counts)
- No lint or typecheck violations

### 3. Only then claim completion

Do not claim "done", "fixed", or "passing" until verification output confirms it. Cite the evidence (command output, test counts, lint status) when reporting completion.

## For This Project

The CI workflow (`.github/workflows/build.yml`) runs on push/PR to `main` and executes restore, build, test (`.NET` + Angular), and Angular production build. Mirror this locally before claiming work complete.

## Principles

- Evidence before assertions — never claim success without running the commands
- Confirm actual output, not assumed output
- Run the full verification suite, not just the changed module
- Fix any failures before claiming completion
