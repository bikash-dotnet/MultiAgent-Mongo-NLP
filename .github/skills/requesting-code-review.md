---
name: requesting-code-review
description: Use when completing tasks, implementing major features, or before merging to verify work meets requirements.
---

# Requesting Code Review

Use this skill when completing tasks, implementing major features, or before merging to verify work meets requirements.

## Process

### 1. Inspect the current state

Before requesting review, inspect:

- `git status`
- `git diff`
- `git log --oneline -10`
- Recent commits and the diff from the base branch

### 2. Stage only intended files

Never commit secrets. Stage only the files you intend to change.

### 3. Write a concise commit message

Match the repo style. Review all commits included in the PR, not just the latest.

### 4. Create the PR

Use `gh` for GitHub tasks. Review the PR diff, status checks, and remote tracking before submitting.

### 5. Verify work meets requirements

Confirm the implementation matches the spec/design and acceptance criteria. Run verification commands (build, tests, lint) before requesting review.

## For This Project

- CI: `.github/workflows/build.yml` runs `.NET` build/test and Angular build/test on push/PR to `main`
- Commit style: concise, conventional-commit-like messages (e.g. `feat:`, `fix:`, `chore:`)
- Do not commit unless explicitly requested by the user

## Principles

- Evidence before assertions — confirm tests pass before requesting review
- Review the full PR diff, not just the latest commit
- Never commit secrets or keys
- Do not force-push, skip hooks, or create empty commits unless requested
