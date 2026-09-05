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
