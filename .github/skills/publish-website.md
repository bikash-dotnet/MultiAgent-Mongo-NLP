---
name: publish-website
description: Publish a Web project as an online hosted application. Retrieves environment info, detects project type, builds frontend assets, generates metadata, packages static assets, and uploads to the showcase API via multipart form POST.
---

# Publish Website

Package the site and upload it via the showcase API to publish it as an online hosted application that is publicly visible.

## Trigger Condition

Use this skill **only** when the user explicitly requests to publish using the `publish-website` skill (or a semantically equivalent explicit instruction mentioning `publish-website`). Vague words like "publish", "go live", or "release" do **not** qualify; use `deploy-website` instead for those.

## Workflow

### 1. Retrieve environment information

Collect project metadata needed for packaging and upload.

### 2. Detect project type

Reuse the detection rules from `deploy-website`:

- Purely static projects (HTML/JS/CSS)
- Node.js frontends
- Containerized backend projects

### 3. Build frontend assets

For Node.js frontends, run the production build (e.g. `npm run build -- --configuration production`).

### 4. Generate metadata

Automatically generate metadata based on application content (title, description, screenshots, tech stack).

### 5. Package static assets

Package the built static assets for upload.

### 6. Upload to showcase API

Upload via multipart form POST to the showcase API. After submission, the site is not directly accessible and requires administrator review.

## Notes

- This skill publishes formal applications; submission requires admin review.
- Do not use for development or testing scenarios — use `deploy-website` instead.
- Do not proactively suggest this skill.
- Do not infer use from vague wording; it must be explicitly requested.
