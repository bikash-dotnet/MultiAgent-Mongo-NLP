# Deployment Runbook

## Prerequisites
- VPS with Docker Engine and Compose v2 installed
- SSH deploy key registered as GitHub secret `SSH_PRIVATE_KEY`
- GitHub Environments configured: `staging` and `production`
- GHCR repository `ghcr.io/bikash-dotnet/MultiAgent-Mongo-NLP`

## First-Time Deploy

```bash
ssh deploy@<vps-host>
mkdir -p /opt/deploy && cd /opt/deploy
# Write .env from GitHub secrets (chmod 600)
docker compose pull
docker compose up -d
```

## Every Release

1. Merge PR to `main`, tag `vX.Y.Z`.
2. CI runs gateway tests, web tests, secret scan, image scan.
3. CD builds images, pushes to GHCR, deploys to staging automatically.
4. Verify staging smoke tests:
   - `GET /health` returns `healthy`
   - `GET /api/session/greeting` returns JWT-gated time-aware message
   - SPA loads and renders `/admin` analytics
5. Approve `production` environment in GitHub Actions.
6. CD deploys to production.

## Smoke Tests

```bash
curl -sSf http://<host>/health
curl -sSf http://<host>/api/session/greeting \
  -H "Authorization: Bearer <jwt>"
curl -sSf http://<host>/admin
```

## Rollback

```bash
cd /opt/deploy
docker compose pull
docker compose up -d --remove-orphans
```

Expected downtime: < 30 seconds.

## Secrets Management

- No secrets in images, workflows, or committed `.env`.
- GitHub Actions secrets write to VPS `.env` at deploy time with `chmod 600`.
- JWT signing key rotates via secret rotation in GitHub; no image rebuild required.
