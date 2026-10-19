using Gateway.Audit;
using Gateway.Auth;
using Gateway.Mcp;
using Gateway.Schemas;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Gateway.Handlers;

public static class McpServerHandlers
{
    public static async Task<IResult> List(
        IMcpServerRegistry registry,
        IMcpHost host,
        CancellationToken ct)
    {
        var servers = await registry.ListAsync(ct);
        IReadOnlyList<McpServerSummary> summaries = servers
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(s => new McpServerSummary(
                s.Id,
                s.Name,
                s.Transport,
                s.Scope,
                s.Enabled,
                s.Enabled ? host.GetHealth(s.Id) : McpHealth.Disabled,
                s.AllowedTools.Count,
                s.UpdatedAt))
            .ToList();
        return Results.Ok(summaries);
    }

    public static async Task<IResult> Get(
        string id,
        IMcpServerRegistry registry,
        IMcpHost host,
        IMcpSecretProbe secrets,
        CancellationToken ct)
    {
        var server = await registry.GetAsync(id, ct);
        if (server is null)
        {
            return Results.NotFound(new { error = $"MCP server '{id}' not found." });
        }

        return Results.Ok(ToDetail(server, host, secrets));
    }

    public static async Task<IResult> Create(
        McpServerRequest request,
        ClaimsPrincipal user,
        IMcpServerRegistry registry,
        IAuditLogStore audit,
        CancellationToken ct)
    {
        var userId = UserId(user);
        var definition = FromRequest(request, request.Id ?? McpServerDefinition.NewId(), 1);
        var error = McpServerValidator.Validate(definition);
        if (error is not null)
        {
            return Results.BadRequest(new { error });
        }

        var stored = await registry.CreateAsync(definition, userId, ct);
        await Audit(audit, user, "mcp_server.create", stored, ct);
        return Results.Ok(stored);
    }

    public static async Task<IResult> Update(
        string id,
        McpServerRequest request,
        ClaimsPrincipal user,
        IMcpServerRegistry registry,
        IAuditLogStore audit,
        CancellationToken ct)
    {
        var userId = UserId(user);
        var current = await registry.GetAsync(id, ct);
        if (current is null)
        {
            return Results.NotFound(new { error = $"MCP server '{id}' not found." });
        }

        if (current.Version != request.Version)
        {
            return Results.Conflict(new { error = $"MCP server '{id}' has been modified by another client." });
        }

        var definition = FromRequest(request, id, request.Version);
        var error = McpServerValidator.Validate(definition);
        if (error is not null)
        {
            return Results.BadRequest(new { error });
        }

        try
        {
            var stored = await registry.SaveAsync(definition, userId, ct);
            await Audit(audit, user, "mcp_server.update", stored, ct);
            return Results.Ok(stored);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
    }

    public static async Task<IResult> Delete(
        string id,
        ClaimsPrincipal user,
        IMcpServerRegistry registry,
        IAuditLogStore audit,
        CancellationToken ct)
    {
        var current = await registry.GetAsync(id, ct);
        if (current is null)
        {
            return Results.NotFound(new { error = $"MCP server '{id}' not found." });
        }

        await registry.DeleteAsync(id, ct);
        await Audit(audit, user, "mcp_server.delete", current, ct);
        return Results.NoContent();
    }

    public static Task<IResult> Enable(
        string id,
        ClaimsPrincipal user,
        IMcpServerRegistry registry,
        IAuditLogStore audit,
        CancellationToken ct) =>
        SetEnabled(id, true, "mcp_server.enable", user, registry, audit, ct);

    public static Task<IResult> Disable(
        string id,
        ClaimsPrincipal user,
        IMcpServerRegistry registry,
        IAuditLogStore audit,
        CancellationToken ct) =>
        SetEnabled(id, false, "mcp_server.disable", user, registry, audit, ct);

    public static async Task<IResult> Test(
        string id,
        ClaimsPrincipal user,
        IMcpServerRegistry registry,
        IMcpHost host,
        IAuditLogStore audit,
        CancellationToken ct)
    {
        var server = await registry.GetAsync(id, ct);
        if (server is null)
        {
            return Results.NotFound(new { error = $"MCP server '{id}' not found." });
        }

        var result = await host.TestAsync(server, ct);
        await Audit(audit, user, "mcp_server.test", server, ct, result.Ok ? "ok" : result.Error, result.LatencyMs);
        return Results.Ok(result);
    }

    public static async Task<IResult> Tools(
        string id,
        IMcpServerRegistry registry,
        IMcpHost host,
        CancellationToken ct)
    {
        var server = await registry.GetAsync(id, ct);
        if (server is null)
        {
            return Results.NotFound(new { error = $"MCP server '{id}' not found." });
        }

        var tools = await host.ListToolsAsync(server, ct);
        return Results.Ok(tools);
    }

    private static async Task<IResult> SetEnabled(
        string id,
        bool enabled,
        string action,
        ClaimsPrincipal user,
        IMcpServerRegistry registry,
        IAuditLogStore audit,
        CancellationToken ct)
    {
        try
        {
            var stored = enabled
                ? await registry.EnableAsync(id, UserId(user), ct)
                : await registry.DisableAsync(id, UserId(user), ct);
            await Audit(audit, user, action, stored, ct);
            return Results.Ok(stored);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound(new { error = $"MCP server '{id}' not found." });
        }
    }

    private static McpServerDefinition FromRequest(McpServerRequest request, string id, int version)
    {
        var transport = Enum.Parse<McpTransport>(request.Transport, ignoreCase: true);
        var scope = Enum.Parse<McpScope>(request.Scope, ignoreCase: true);
        return new McpServerDefinition(
            id,
            request.Name,
            request.Description,
            transport,
            request.Command,
            request.Args ?? [],
            request.Url,
            request.EnvKeys ?? [],
            request.Enabled,
            request.AllowedTools ?? [],
            request.ReadOnly,
            request.TimeoutMs <= 0 ? 5000 : request.TimeoutMs,
            scope,
            request.ScopeValue,
            version,
            null,
            null);
    }

    private static McpServerDetail ToDetail(McpServerDefinition server, IMcpHost host, IMcpSecretProbe secrets)
    {
        var env = server.EnvKeys
            .Select(key => new McpEnvKeyStatus(key, secrets.IsConfigured(key)))
            .ToList();
        return new McpServerDetail(
            server.Id,
            server.Name,
            server.Description,
            server.Transport,
            server.Command,
            server.Args,
            server.Url,
            env,
            server.Enabled,
            server.AllowedTools,
            server.ReadOnly,
            server.TimeoutMs,
            server.Scope,
            server.ScopeValue,
            server.Version,
            server.UpdatedAt,
            server.UpdatedBy,
            server.Enabled ? host.GetHealth(server.Id) : McpHealth.Disabled,
            host.LastCheckedAt(server.Id),
            host.LastError(server.Id));
    }

    private static string UserId(ClaimsPrincipal user) =>
        user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";

    private static Task Audit(
        IAuditLogStore audit,
        ClaimsPrincipal user,
        string action,
        McpServerDefinition server,
        CancellationToken ct,
        string? notes = null,
        long durationMs = 0)
    {
        var userId = UserId(user);
        var detail = notes is null
            ? $"{server.Id} transport={server.Transport}"
            : $"{server.Id} transport={server.Transport} {notes} {durationMs}ms";
        return audit.AppendAsync(
            AuditLogDocument.ForSchemaManagement(
                Guid.NewGuid().ToString("N"),
                DateTimeOffset.UtcNow,
                userId,
                SchemaDataSource.Mongo,
                action,
                server.Version,
                detail),
            ct);
    }
}
