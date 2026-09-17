# MultiAgent-Mongo-NLP

Enterprise multi-agent natural language query and governance platform.

- BRD: `docs/BRD.md` (v4.2, ASP.NET Core 10 / Angular 21 / Semantic Kernel)
- Archived Python BRD: `docs/BRD_V1.md`
- Sprints: `sprints/sprint-0.md`

## Sprint 1 local run

```bash
# Gateway
dotnet test tests/Gateway.Tests/Gateway.Tests.csproj
dotnet run --project src/gateway --urls http://localhost:5235

# SPA (proxies /api to the gateway)
cd src/web
npm start
```

Open the Angular dev server. The SPA fetches a Development token from `/dev/token`, then loads `GET /api/session/greeting` and an SSE heartbeat from `GET /api/agents/stream`.

## Executive showcase

`showcase/index.html` is a self-contained, executive-facing walkthrough of the platform: an executive summary plus a six-act story that follows a single request from question to governed answer. Open it by double-clicking the file; it needs no server, build step, or network access.

All copy lives in the `DECK` object at the top of the file so the same content can drive a future slide deck. Run `node showcase/verify.mjs` to check the file against the showcase constraints (no emojis, no citation artifacts, no placeholders, no external assets, and status pills present).
