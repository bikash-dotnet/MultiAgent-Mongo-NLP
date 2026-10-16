---
name: deploy-website
description: Deploy and locally preview Web projects. Automatically detects project type (Node.js, static HTML, .NET, Python, Go, etc.) and starts the corresponding development server in a background terminal, then requests a preview URL.
---

# Deploy Website

Automatically detect the project type and start the corresponding development server in the background, then obtain an accessible preview link.

## Trigger Conditions

Execute website deployment when any of the following is met:

1. **Static HTML files** are created or modified
2. **Node.js Web project** — `package.json` with Web-related scripts (`dev`, `start`, `serve`)
3. **The user attempts to start a frontend or backend service** — `npm run dev`, `npm start`, `dotnet run`, `python manage.py runserver`, etc.

## Supported Project Types

| Type | Detection | Start Command |
|------|-----------|---------------|
| Node.js | `package.json` with `dev`/`start` script | `npm run dev` or `npm start` |
| Static HTML | `.html` files (esp. `index.html`) | `python3 -m http.server 8000` |
| .NET / ASP.NET Core | `.csproj`, `Program.cs` | `dotnet run --project <path>` |
| Python/Django | `manage.py` + `settings.py` | `python manage.py runserver 8000` |
| Python/Flask | `app.py` or `wsgi.py` | `flask run --port=8000` or `python app.py` |
| Go | `go.mod` | `go run .` or `go run main.go` |

## Workflow

### 1. Check for an already-running server

Before starting a new server, call `background_terminal_list` to check for running background terminals, and check whether a process is already listening on the target port:

```bash
ss -tlnp 2>/dev/null || netstat -tlnp 2>/dev/null || lsof -iTCP -sTCP:LISTEN -P -n
```

If a server is already running, extract the port, skip startup, and call `request_preview` with that port.

### 2. Consult project documentation first

Before using default detection, read project docs to confirm the startup method, in this priority order:

1. `.monkeycode/MEMORY.md`
2. `README.md`
3. `AGENTS.md`

If documentation specifies a startup command, use it. Otherwise fall back to default detection.

### 3. Start the server with `background_terminal_create`

Use `background_terminal_create` (not `&` or `timeout`) so the server runs without blocking the session and its output is logged.

For this project:

- **Angular SPA** (`src/web`): `npm ci` then `npm start` (proxies `/api`, `/hubs`, `/dev` to `http://127.0.0.1:5235` via `proxy.conf.json`)
- **.NET Gateway** (`src/gateway`): `dotnet run --project src/gateway --urls http://localhost:5235`
- **Static showcase** (`showcase/index.html`): `python3 -m http.server 8000`

### 4. Confirm startup via logs

Get the log path with `background_terminal_output_path`, read it, and look for readiness signals such as `Listening on`, `Server running`, `ready in`, or `Local: http://`.

### 5. Get the preview URL

Call `request_preview` with the server port to obtain the preview `access_url`.

### 6. Verify preview connectivity

```bash
curl -s --max-time 10 "$ACCESS_URL/.well-known/mcai-preview-connect-status-detect" | head -n10
```

If the response does **not** contain `<meta name="mcai-preview-error"`, the preview connection is normal.

### 7. Present the preview

Show the clickable preview URL to the user and provide the background terminal ID for later management.

### 8. Stop the server

Use `background_terminal_kill <terminal_id>`. Do **not** use `pkill` or `killall`.
