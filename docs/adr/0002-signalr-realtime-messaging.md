# ADR 0002: Migration from SSE to ASP.NET Core SignalR for Real-Time Multi-Agent Streaming

## Context

The system previously used HTTP Server-Sent Events (SSE) via `EventSource` (`GET /api/agents/stream`) to deliver real-time multi-agent execution events (such as `agent.idle`, `agent.started`, `agent.executing`, `governance.paused`, `report.ready`) from the ASP.NET Core gateway to the Angular 21 SPA.

While SSE is a lightweight unidirectional protocol, it presented several operational limitations in local development and proxy environments:
1. **Proxy Response Buffering**: Standard HTTP proxies (such as Angular CLI's `http-proxy` and local web servers) buffer SSE GET chunks until internal buffer thresholds are met, causing client `EventSource` connections to sit in a `CONNECTING` state for several seconds.
2. **Unidirectional Transport**: SSE only supports server-to-client streaming, preventing future bidirectional real-time interactions (e.g. client cancellation, live input streaming, and interactive clarification).
3. **Reconnection State Management**: Raw `EventSource` requires custom reconnect delay logic and does not provide native hub lifecycle states (`Connecting`, `Connected`, `Reconnecting`, `Disconnected`).

## Decision

We will migrate real-time messaging from SSE to **ASP.NET Core SignalR** (`Microsoft.AspNetCore.SignalR`).

### Key Architectural Changes

1. **Backend Gateway (`src/gateway`)**:
   - Introduce `AgentHub : Hub<IAgentHubClient>` mapped at `/hubs/agent-stream`.
   - Use typed client interfaces (`IAgentHubClient`) exposing `ReceiveAgentEvent(string eventName, string status, string detail)`.
   - Update `JwtBearerEvents.OnMessageReceived` in `Program.cs` to extract `access_token` query parameters for SignalR WebSockets handshakes.
   - Inject `IHubContext<AgentHub, IAgentHubClient>` into `IAgentEventSink` / `InMemoryAgentEventSink` so events published by orchestrators automatically dispatch to connected SignalR clients.

2. **Frontend SPA (`src/web`)**:
   - Install `@microsoft/signalr` package.
   - Refactor `AgentStreamService` to use `HubConnectionBuilder` with `.withUrl('/hubs/agent-stream', ...)` and `.withAutomaticReconnect()`.
   - Update `proxy.conf.json` to enable WebSocket proxying (`"ws": true`).

3. **Event Contract & Greeting Flow**:
   - Maintain full backwards compatibility with existing event names (`agent.idle`, `agent.started`, `agent.executing`, `governance.paused`, `report.ready`, etc.).
   - Upon SignalR connection establishment (`Connected` state / `agent.idle` event), automatically populate the assistant greeting message in `ChatThreadComponent`.

## Consequences

- **Instant Connection**: WebSockets upgrade (`101 Switching Protocols`) bypasses HTTP proxy buffering completely, reducing connection latency to < 10ms locally.
- **Bidirectional Capabilities**: Establishes a foundation for bidirectional client-agent communication in future sprints.
- **Reliable Lifecycle**: SignalR provides built-in automatic reconnect, connection state events, and fallback transports (WebSockets -> Server-Sent Events -> Long Polling).
