---
name: writing-skills
description: Use when creating new skills, editing existing skills, or verifying skills work before deployment. A skill is a SKILL.md file with YAML frontmatter (name, description, optional arguments) plus markdown documentation.
---

# Writing Skills

Use this skill when creating new skills, editing existing skills, or verifying skills work before deployment.

## Skill Format

Each skill is a markdown file (conventionally `SKILL.md`) with:

### YAML Frontmatter

```yaml
---
name: <skill-name>
description: <one-line description>
arguments:
  - name: <arg-name>
    description: <arg description>
    required: <true|false>
---
```

- `name` — unique skill identifier
- `description` — concise one-line description of purpose
- `arguments` — optional list of named arguments with `description` and `required`

### Markdown Body

- `# <Title>` as the first heading
- Sections explaining purpose, trigger conditions, workflow, and usage
- Code blocks fenced with language identifiers
- No inline end-of-line shell comments (place comments on a separate line above the command)

## For This Project

Skill files are stored in `.github/skills/`. This project includes skill docs for:

- `deploy-website.md` — preview/deploy the Angular SPA and static showcase
- `publish-website.md` — publish the executive showcase via the showcase API
- `ui-ux-pro-max.md` — UI/UX design for the Angular + PrimeNG frontend
- `test-driven-development.md` — TDD for .NET gateway and Angular vitest tests
- `systematic-debugging.md` — debugging bugs and test failures
- `verification-before-completion.md` — verify before claiming done
- `writing-plans.md` — multi-step implementation planning
- `brainstorming.md` — design before implementation
- `requesting-code-review.md` — PR/merge review
- `writing-skills.md` — creating and editing skills (this file)

## Principles

- Match the project's coding conventions and existing skill patterns
- Keep descriptions concise and accurate
- Include trigger conditions so the skill is invoked appropriately
- Verify each skill works before considering it complete
- Do not hardcode credentials or secrets into skill files
