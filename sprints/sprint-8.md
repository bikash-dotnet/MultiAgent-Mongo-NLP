# Sprint 8 — CI/CD Pipeline (GitHub Actions + Docker Compose)

**Duration:** 2 weeks  
**Sprint goal:** Automate build, test, image publish, and zero-downtime deployment of the gateway and SPA to a Linux VPS via GitHub Actions and Docker Compose.

**BRD version:** 4.2  
**Depends on:** Sprint 7

---

## Traceability

CI/CD is a platform concern rather than a single BRD feature. It exists to make the BRD NFR set observable and safe to ship.

| BRD ID | Priority | Coverage in this sprint |
| --- | --- | --- |
| BRD-NFR-08 | — | Container and image scan gates run before deploy; execution stays read-only |
| BRD-NFR-11 | — | Smoke test verifies the SSE greeting path on the deployed environment |
| BRD-NFR-12 | — | JWT signing key and audience/issuer flow from secrets, never from image layers |
| BRD-NFR-03 | — | Post-deploy smoke test asserts the 5,000 ms timeout config is present |

---

## Tech stack

- CI: GitHub Actions (`ubuntu-latest` runners)
- Images: `docker/build-push-action` to GitHub Container Registry (GHCR), `docker/buildx`
- Runtime: Docker Compose on a Linux VPS, `nginx` serving the SPA and reverse-proxying `/api`, `/health`, `/dev`
- Base images follow the BRD v4.2 runtime floor: `mcr.microsoft.com/dotnet/aspnet:10.0`, `node:22`, `nginx:1.27-alpine`
- Secrets: GitHub Actions encrypted secrets -> VPS `.env` (never committed)

---

## Pipeline overview

```text
[push / PR] -> CI workflow
                |- gateway: restore -> build -> dotnet test tests/Gateway.Tests
                |- web:      npm ci -> ng test -> ng build --configuration production
                |- image scan / secret scan gates
                          |
                          v
[tag vX.Y.Z] -> CD workflow (staging, then production on approval)
                |- buildx -> push gateway + web images to GHCR
                |- ssh -> pull new images -> docker compose up -d
                |- post-deploy smoke: GET /health, GET /api/session/greeting (JWT)
                |- rollback = docker compose up -d <previous image tag>
```

---

## Stories

### S8-01 Gateway image (`src/gateway/Dockerfile`)
- Multi-stage build: `mcr.microsoft.com/dotnet/sdk:10.0` -> `mcr.microsoft.com/dotnet/aspnet:10.0`
- Publish `Release` for `net10.0` (track the `Gateway.csproj` TFM); runtime image is non-root
- `HEALTHCHECK` against `GET /health`; no MongoDB URI, NVIDIA key, or JWT secret baked into any layer
- App binds to `http://+:8080` inside the container; port mapped only by Compose

### S8-02 SPA image + nginx (`src/web/Dockerfile`, `deploy/nginx.conf`)
- Multi-stage: `node:22` runs `npm ci` and `ng build --configuration production`
- Static output copied into `nginx:1.27-alpine`; assets gzip-cached, SPA fallback to `index.html`
- nginx reverse-proxies `/api`, `/health`, `/dev` to the `gateway` Compose service; no direct client-to-gateway exposure

### S8-03 CI workflow (`.github/workflows/ci.yml`)
- Triggers: `pull_request` and `push` to `main`
- Gateway job: `dotnet restore`, `dotnet build -c Release`, `dotnet test` on `tests/Gateway.Tests`
- Web job: `npm ci`, `ng test` (Chromium headless), `ng build --configuration production`
- Security gates: `gitleaks` secret scan and container image scan (e.g., Trivy) on produced images; failure blocks merge
- Uploads build/test artifacts; runs in parallel jobs with a `required` check-set protecting `main`

### S8-04 CD workflow (`.github/workflows/cd.yml`)
- Triggers: semver tag `v*.*.*` for staging; explicit production approval on the staging environment
- Jobs: `docker/build-push-action` (buildx cache, GHCR push, `sha-<short>` + semver tags), then deploy
- Matrix of environments `staging` -> `production`, each with its own VPS host + `.env`
- Deploy via SSH runner (not tunnel): copy Compose file and `.env`, pull new images, `docker compose up -d --remove-orphans`

### S8-05 Compose topology (`deploy/docker-compose.yml`)
- Services: `gateway`, `web` (nginx); optional `mongo` profile for dev only, never for production listings
- Networks isolate `web` -> `gateway`; gateway reaches Mongo/NVIDIA only via server-side config
- Named volume for `audit_logs`-adjacent state if required; `restart: unless-stopped`
- Health checks per service; Compose waits on gateway health before nginx starts

### S8-06 Environment configuration (`deploy/.env.example`)
- Committed template with placeholders only: `JWT__SigningKey`, `Mongo__ConnectionString`, `NVIDIA__ApiKey`, `Issuer`, `Audience`, `ASPNETCORE_ENVIRONMENT`
- Real values live in GitHub Actions secrets -> written to VPS `.env` (chmod 600) during CD
- `appsettings.Production.json` overrides URLs, logging, and timeouts (enforces BRD-NFR-03)

### S8-07 Deployment runbook (`docs/deploy/runbook.md`)
- Prerequisites: VPS with Docker Engine + Compose v2, SSH deploy key registered as a GitHub secret
- Steps: first-time `docker compose up -d` from empty state; every release is `pull + up -d`
- Post-deploy smoke test asserts: `200` on `/health`, JWT greeting returns a time-aware message, SPA loads
- Rollback procedure: re-point to the previous image tag and `docker compose up -d`; document expected downtime < 30 s

### S8-08 Release & env promotion
- Cut a release: merge to `main`, tag `vX.Y.Z`; changelog generated from `feat/fix/docs` commits
- Staging deploys automatically on tag; production requires an environment approval gate in Actions
- Every deploy records commit SHA, image digest, and environment in the release notes for auditability

---

## Acceptance criteria

- [ ] `git push` to a PR branch runs gateway restore/build/test and web `npm ci`/`ng test`/`ng build` in CI (BRD-NFR-08 gate)
- [ ] New tag `v*.*.*` builds both images and pushes them to GHCR
- [ ] Staging deploys automatically; production deploy is blocked until an explicit approval
- [ ] `docker compose up -d` on a clean VPS brings up the SPA reachable at the host, proxying `/api` and `/health` to the gateway
- [ ] Post-deploy smoke test passes: `/health` returns healthy, greeting endpoint issues a valid JWT-gated response, SSE greeting streams
- [ ] No secret appears in any image layer, workflow log, or committed `.env`; secrets are injected at container runtime
- [ ] Rollback to the previous image tag is documented and demonstrated on staging

---

## Out of scope (platform, per BRD §14)

- Kubernetes or cloud-managed orchestration (AKS / ECS / App Service)
- Database migrations or schema changes during deploy (no write operations)
- Multi-region failover, autoscaling, or load balancer provisioning
- Direct client access to MongoDB or NVIDIA (unchanged: all traffic through the ASP.NET Core 10 Gateway)

---

## Exit artifacts

- `src/gateway/Dockerfile`, `src/web/Dockerfile`, `deploy/nginx.conf`
- `.github/workflows/ci.yml`, `.github/workflows/cd.yml`
- `deploy/docker-compose.yml`, `deploy/.env.example`
- `docs/deploy/runbook.md` with deploy, smoke test, and rollback procedures
- Two environments (staging, production) deployable from the same pipeline with an approval gate
