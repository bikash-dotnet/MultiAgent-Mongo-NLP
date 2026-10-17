using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gateway.Analytics;
using Gateway.Audit;
using Gateway.Auth;
using Gateway.Conversations;
using Gateway.Execution;
using Gateway.Governance;
using Gateway.Greeting;
using Gateway.Hubs;
using Gateway.Nlp;
using Gateway.Nlp.Guardrails;
using Gateway.Nlp.Http;
using Gateway.Nlp.Orchestrator;
using Gateway.Nlp.Router;
using Gateway.Observability;
using Gateway.Reports;
using Gateway.Schemas;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddCors(options =>
{
    options.AddPolicy("spa", policy =>
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is required.");
var issuer = jwtSection["Issuer"] ?? "MultiAgentMongoNlp";
var audience = jwtSection["Audience"] ?? "MultiAgentMongoNlp.Spa";

JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = SessionClaims.Name,
            RoleClaimType = SessionClaims.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"].ToString();
                var path = context.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/api/agents/stream") || path.StartsWithSegments("/hubs/agent-stream")))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddSignalR();
builder.Services.AddOpenApi();
builder.Services.AddGatewayNlp(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<SessionCorrelationMiddleware>();
app.UseCors("spa");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    app.MapGet("/dev/token", (IConfiguration config) =>
    {
        var key = config["Jwt:SigningKey"] ?? signingKey;
        var tokenIssuer = config["Jwt:Issuer"] ?? issuer;
        var tokenAudience = config["Jwt:Audience"] ?? audience;
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(SessionClaims.UserId, "usr_bn_101"),
            new Claim(SessionClaims.Name, "Bikash"),
            new Claim(SessionClaims.Email, "bnayak@enterprise.com"),
            new Claim(SessionClaims.Role, "Data Owner / Admin"),
            new Claim(SessionClaims.LeadUserId, "usr_bn_101")
        };
        var jwt = new JwtSecurityToken(
            tokenIssuer,
            tokenAudience,
            claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);
        return Results.Ok(new { token = new JwtSecurityTokenHandler().WriteToken(jwt) });
    });

    app.MapPost("/dev/enterprise-core/query", (EnterpriseCoreQueryRequest body) =>
    {
        var columns = body.Columns is { Count: > 0 } list
            ? list
            : new List<string> { "name", "address.market", "price" };
        var request = new ExecutionRequest(
            body.Query ?? "[]",
            "dev",
            "dev",
            ExecutionDataSource.EnterpriseCoreREST,
            body.Collection ?? "listingsAndReviews",
            columns,
            new Dictionary<string, string>(),
            false,
            false,
            0,
            new RequesterContext("dev", "dev", "Business Analyst", null),
            GovernanceDecision.None);
        var result = DemoTabularSource.Build(request);
        return Results.Ok(new { columns = result.Columns, rows = result.Rows });
    });
}

app.MapGet("/api/session/greeting", (ClaimsPrincipal user, TimeProvider clock) =>
{
    var name = SessionClaims.DisplayName(user);
    var period = GreetingClock.ResolvePeriod(TimeOnly.FromTimeSpan(clock.GetLocalNow().TimeOfDay));
    var chips = new[]
    {
        "Listings in Los Angeles",
        "Pools under $200",
        "Just run it"
    };
    return Results.Ok(new GreetingResponse(name, period, GreetingClock.FormatMessage(name, period), chips));
}).RequireAuthorization();

app.MapPost("/api/nlp/query", async (NlpQueryRequest request, ClaimsPrincipal user, INlpOrchestrator orchestrator, CancellationToken cancellationToken) =>
{
    var utterance = request.Utterance?.Trim();
    if (string.IsNullOrWhiteSpace(utterance))
    {
        return Results.BadRequest(new { error = "utterance is required" });
    }

    if (utterance.Length > 500)
    {
        return Results.BadRequest(new { error = "utterance must be 500 characters or fewer" });
    }

    var sessionId = user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";
    var result = await orchestrator.OrchestrateAsync(utterance, sessionId, cancellationToken, SessionClaims.ToRequesterContext(user));
    return Results.Ok(NlpQueryResponse.From(result));
}).RequireAuthorization();

app.MapPost("/api/conversations", async (
    ConversationStartRequest request,
    ClaimsPrincipal user,
    ConversationOrchestrator conversations,
    CancellationToken cancellationToken) =>
{
    var utterance = request.Utterance?.Trim();
    if (string.IsNullOrWhiteSpace(utterance))
    {
        return Results.BadRequest(new { error = "utterance is required" });
    }

    if (utterance.Length > 500)
    {
        return Results.BadRequest(new { error = "utterance must be 500 characters or fewer" });
    }

    var sessionId = user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";
    var email = user.FindFirst(SessionClaims.Email)?.Value;
    var turn = await conversations.StartAsync(utterance, sessionId, email, cancellationToken, SessionClaims.ToRequesterContext(user));
    return Results.Ok(turn);
}).RequireAuthorization();

app.MapPost("/api/conversations/{id}/answers", async (
    string id,
    ConversationAnswer answer,
    ConversationOrchestrator conversations,
    CancellationToken cancellationToken) =>
{
    var turn = await conversations.AnswerAsync(id, answer, cancellationToken);
    return turn is null ? Results.NotFound() : Results.Ok(turn);
}).RequireAuthorization();

app.MapGet("/api/conversations/{id}", async (
    string id,
    ConversationOrchestrator conversations,
    CancellationToken cancellationToken) =>
{
    var turn = await conversations.GetAsync(id, cancellationToken);
    return turn is null ? Results.NotFound() : Results.Ok(turn);
}).RequireAuthorization();

app.MapGet("/api/conversations/{id}/report.csv", async (
    string id,
    ConversationOrchestrator conversations,
    CancellationToken cancellationToken) =>
{
    var turn = await conversations.GetAsync(id, cancellationToken);
    if (turn is null)
    {
        return Results.NotFound();
    }

    if (turn.Step != "Complete")
    {
        return Results.Conflict(new { error = "conversation is not complete" });
    }

    var columns = turn.Columns?.Where(column => column.Selected).Select(column => column.Name).ToList() ?? [];
    var build = turn.Execution is not null
        ? ReportService.BuildCsv(turn.Execution.Rows, columns)
        : ReportService.BuildCsv(columns, turn.ApprovalRequired);
    if (!build.Ready)
    {
        return Results.Conflict(new { error = build.Reason });
    }

    return Results.Text(build.Csv, "text/csv");
}).RequireAuthorization();

app.MapGet("/api/conversations/{id}/report.xlsx", async (
    string id,
    ConversationOrchestrator conversations,
    ExportDeliveryService exports,
    CancellationToken cancellationToken) =>
{
    var turn = await conversations.GetAsync(id, cancellationToken);
    if (turn is null)
    {
        return Results.NotFound();
    }

    if (turn.Step != "Complete")
    {
        return Results.Conflict(new { error = "conversation is not complete" });
    }

    var columns = turn.Columns?.Where(column => column.Selected).Select(column => column.Name).ToList() ?? [];
    if (turn.Execution is null || columns.Count == 0)
    {
        return Results.Conflict(new { error = "No columns were confirmed for this report." });
    }

    var result = exports.Xlsx(columns, turn.Execution.Rows);
    return Results.File(result.Content, result.ContentType, result.FileName);
}).RequireAuthorization();

app.MapGet("/api/conversations/{id}/report.pdf", async (
    string id,
    ConversationOrchestrator conversations,
    ExportDeliveryService exports,
    CancellationToken cancellationToken) =>
{
    var turn = await conversations.GetAsync(id, cancellationToken);
    if (turn is null)
    {
        return Results.NotFound();
    }

    if (turn.Step != "Complete")
    {
        return Results.Conflict(new { error = "conversation is not complete" });
    }

    var columns = turn.Columns?.Where(column => column.Selected).Select(column => column.Name).ToList() ?? [];
    IReadOnlyList<IReadOnlyDictionary<string, string?>> rows = turn.Execution?.Rows ?? [];
    var briefing = NarrativeInsights.Standard(columns, rows);
    var result = exports.Pdf("Report briefing", briefing);
    return Results.File(result.Content, result.ContentType, result.FileName);
}).RequireAuthorization();

app.MapPost("/api/conversations/{id}/report/briefing", async (
    string id,
    ConversationOrchestrator conversations,
    CancellationToken cancellationToken) =>
{
    var turn = await conversations.GetAsync(id, cancellationToken);
    if (turn is null)
    {
        return Results.NotFound();
    }

    var columns = turn.Columns?.Where(column => column.Selected).Select(column => column.Name).ToList() ?? [];
    IReadOnlyList<IReadOnlyDictionary<string, string?>> rows = turn.Execution?.Rows ?? [];
    return Results.Ok(NarrativeInsights.Standard(columns, rows));
}).RequireAuthorization();

app.MapPost("/api/conversations/{id}/report/email", async (
    string id,
    EmailBriefingRequest? body,
    ConversationOrchestrator conversations,
    ExportDeliveryService exports,
    CancellationToken cancellationToken) =>
{
    var turn = await conversations.GetAsync(id, cancellationToken);
    if (turn is null)
    {
        return Results.NotFound();
    }

    var recipient = string.IsNullOrWhiteSpace(body?.Recipient) ? turn.EmailPrefill : body!.Recipient;
    if (string.IsNullOrWhiteSpace(recipient))
    {
        return Results.BadRequest(new { error = "a recipient email is required" });
    }

    var columns = turn.Columns?.Where(column => column.Selected).Select(column => column.Name).ToList() ?? [];
    IReadOnlyList<IReadOnlyDictionary<string, string?>> rows = turn.Execution?.Rows ?? [];
    var briefing = NarrativeInsights.Standard(columns, rows);
    var sent = await exports.EmailBriefingAsync(recipient, "Report briefing", briefing, cancellationToken);
    return Results.Ok(new { sent, recipient, format = "PDF" });
}).RequireAuthorization();

app.MapGet("/api/admin/analytics", async (IAuditLogStore audit, CancellationToken cancellationToken) =>
{
    var documents = await audit.ListAsync(cancellationToken);
    return Results.Ok(AdminAnalytics.Aggregate(documents));
}).RequireAuthorization();

app.MapGet("/api/governance/approval", (IApprovalFlagStore flags) =>
    Results.Ok(new { enabled = flags.Enabled })).RequireAuthorization();

app.MapPut("/api/governance/approval", async (
    ApprovalFlagRequest request,
    ClaimsPrincipal user,
    IApprovalFlagStore flags,
    CancellationToken cancellationToken) =>
{
    var role = user.FindFirst(SessionClaims.Role)?.Value;
    if (role != "Data Owner / Admin")
    {
        return Results.Forbid();
    }

    await flags.SetAsync(request.Enabled, cancellationToken);
    return Results.Ok(new { enabled = flags.Enabled });
}).RequireAuthorization();

app.MapGet("/api/access-requests", async (
    string? status,
    ClaimsPrincipal user,
    IGovernanceService governance,
    CancellationToken cancellationToken) =>
{
    var viewer = SessionClaims.ToRequesterContext(user);
    var requests = await governance.ListAsync(viewer, status, cancellationToken);
    return Results.Ok(requests);
}).RequireAuthorization();

app.MapGet("/api/access-requests/{id}", async (
    string id,
    ClaimsPrincipal user,
    IGovernanceService governance,
    CancellationToken cancellationToken) =>
{
    try
    {
        var viewer = SessionClaims.ToRequesterContext(user);
        return Results.Ok(await governance.GetForViewerAsync(id, viewer, cancellationToken));
    }
    catch (GovernanceNotFoundException)
    {
        return Results.NotFound();
    }
    catch (GovernanceForbiddenException)
    {
        return Results.Forbid();
    }
}).RequireAuthorization();

app.MapPost("/api/access-requests/{id}/approve", async (
    string id,
    GovernanceDecisionRequest? body,
    ClaimsPrincipal user,
    IGovernanceService governance,
    CancellationToken cancellationToken) =>
{
    try
    {
        var actor = SessionClaims.ToRequesterContext(user);
        return Results.Ok(await governance.ApproveAsync(id, actor, body?.Notes, cancellationToken));
    }
    catch (GovernanceNotFoundException)
    {
        return Results.NotFound();
    }
    catch (GovernanceForbiddenException)
    {
        return Results.Forbid();
    }
    catch (GovernanceConflictException error)
    {
        return Results.Conflict(new { error = error.Message });
    }
}).RequireAuthorization();

app.MapPost("/api/access-requests/{id}/reject", async (
    string id,
    GovernanceDecisionRequest? body,
    ClaimsPrincipal user,
    IGovernanceService governance,
    CancellationToken cancellationToken) =>
{
    try
    {
        var actor = SessionClaims.ToRequesterContext(user);
        return Results.Ok(await governance.RejectAsync(id, actor, body?.Notes, cancellationToken));
    }
    catch (GovernanceNotFoundException)
    {
        return Results.NotFound();
    }
    catch (GovernanceForbiddenException)
    {
        return Results.Forbid();
    }
    catch (GovernanceConflictException error)
    {
        return Results.Conflict(new { error = error.Message });
    }
}).RequireAuthorization();

app.MapPost("/api/access-requests/{id}/override", async (
    string id,
    GovernanceDecisionRequest? body,
    ClaimsPrincipal user,
    IGovernanceService governance,
    CancellationToken cancellationToken) =>
{
    try
    {
        var actor = SessionClaims.ToRequesterContext(user);
        return Results.Ok(await governance.OverrideAsync(id, actor, body?.Notes ?? string.Empty, cancellationToken));
    }
    catch (GovernanceNotFoundException)
    {
        return Results.NotFound();
    }
    catch (GovernanceForbiddenException)
    {
        return Results.Forbid();
    }
    catch (GovernanceValidationException error)
    {
        return Results.BadRequest(new { error = error.Message });
    }
    catch (GovernanceConflictException error)
    {
        return Results.Conflict(new { error = error.Message });
    }
}).RequireAuthorization();

app.MapGet("/api/agents/stream", async (HttpContext context, IAgentEventSink events, CancellationToken cancellationToken) =>
{
    context.Response.Headers.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache, no-transform";
    context.Response.Headers.Connection = "keep-alive";
    context.Response.Headers["X-Accel-Buffering"] = "no";
    context.Response.Headers["Pragma"] = "no-cache";

    await WriteAgentEvent(context, new AgentEvent("agent.idle", "idle"), cancellationToken);

    try
    {
        await foreach (var agentEvent in events.ReadAllAsync(cancellationToken))
        {
            await WriteAgentEvent(context, agentEvent, cancellationToken);
        }
    }
    catch (OperationCanceledException)
    {
    }
}).RequireAuthorization();

app.MapHub<AgentHub>("/hubs/agent-stream").RequireAuthorization();

#region Schema Registry API

static bool IsDataOwnerOrAdmin(ClaimsPrincipal user)
{
    var role = user.FindFirst(SessionClaims.Role)?.Value;
    return role == "Data Owner / Admin";
}

static AuditLogDocument BuildSchemaAuditDoc(ClaimsPrincipal user, SchemaDataSource dataSource, string action, string? schemaId, int version, string? notes = null)
{
    var viewer = SessionClaims.ToRequesterContext(user);
    return new AuditLogDocument(
        Guid.NewGuid().ToString("N"),
        DateTimeOffset.UtcNow,
        "schema-management",
        dataSource.ToString(),
        new AuditLogUser(viewer.UserId, viewer.Name, viewer.Email, viewer.Role),
        new AuditLogNlpPerformance(false, false, 0, 0),
        new AuditLogRequestDetails("", System.Collections.Immutable.ImmutableDictionary<string, string>.Empty),
        new AuditLogExecutionDetails("", "schemas", 0, null, null),
        new AuditLogGovernance(false, [], null, false, $"{viewer.Name}: {action} {(schemaId ?? string.Empty)} v{version}" + (notes is not null ? $" ({notes})" : string.Empty)));
}

app.MapGet("/api/schemas", async (
    ISchemaRegistry registry,
    CancellationToken cancellationToken) =>
{
    var schemas = await registry.ListAsync(cancellationToken);
    var summaries = schemas.Select(s => new SchemaSummary(
        s.Id, s.Name, s.DataSource, s.Fields.Count, s.IsActiveSource, s.Version, s.UpdatedAt, s.UpdatedBy));
    return Results.Ok(new SchemaListResponse(summaries.ToList()));
}).RequireAuthorization();

app.MapGet("/api/schemas/{id}", async (
    string id,
    ISchemaRegistry registry,
    CancellationToken cancellationToken) =>
{
    var schema = await registry.GetAsync(id, cancellationToken);
    return schema is null ? Results.NotFound() : Results.Ok(new SchemaResponse(schema));
}).RequireAuthorization();

app.MapPost("/api/schemas", async (
    CreateSchemaRequest request,
    ClaimsPrincipal user,
    ISchemaRegistry registry,
    IAuditLogStore audit,
    ActiveSchemaProvider activeSchema,
    CancellationToken cancellationToken) =>
{
    if (!IsDataOwnerOrAdmin(user))
    {
        return Results.Forbid();
    }

    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new { error = "Schema name is required." });
    }

    if (request.Fields is null || request.Fields.Count == 0)
    {
        return Results.BadRequest(new { error = "At least one field is required." });
    }

    var fieldPaths = request.Fields.Select(f => f.Path).ToList();
    if (fieldPaths.Any(string.IsNullOrWhiteSpace))
    {
        return Results.BadRequest(new { error = "Field path is required for all fields." });
    }

    if (fieldPaths.Distinct(StringComparer.Ordinal).Count() != fieldPaths.Count)
    {
        return Results.BadRequest(new { error = "Duplicate field paths are not allowed." });
    }

    var fields = request.Fields.Select(f => new SchemaField(
        f.Path, f.DataType, f.IsSensitive, f.RequiresApproval, f.DataOwnerRoles ?? [], f.Description)).ToList();

    var existing = await registry.ListAsync(cancellationToken);
    var makeActive = request.IsActiveSource || existing.All(s => !s.IsActiveSource);

    var schema = new SchemaDefinition(
        SchemaDefinition.NewId(),
        request.Name.Trim(),
        request.DataSource,
        request.Database.Trim(),
        request.Collection.Trim(),
        fields,
        makeActive,
        1,
        request.Description,
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow,
        SessionClaims.ToRequesterContext(user).UserId);

    var created = await registry.CreateAsync(schema, cancellationToken);
    if (makeActive)
    {
        await registry.ActivateAsync(created.Id, cancellationToken);
        created = await registry.GetAsync(created.Id, cancellationToken) ?? created;
    }

    await audit.AppendAsync(BuildSchemaAuditDoc(user, request.DataSource, "create", created.Id, created.Version), cancellationToken);
    await activeSchema.RefreshAsync(cancellationToken);
    return Results.Ok(new SchemaResponse(created));
}).RequireAuthorization();

app.MapPut("/api/schemas/{id}", async (
    string id,
    UpdateSchemaRequest request,
    ClaimsPrincipal user,
    ISchemaRegistry registry,
    IAuditLogStore audit,
    ActiveSchemaProvider activeSchema,
    CancellationToken cancellationToken) =>
{
    if (!IsDataOwnerOrAdmin(user))
    {
        return Results.Forbid();
    }

    var current = await registry.GetAsync(id, cancellationToken);
    if (current is null)
    {
        return Results.NotFound();
    }

    if (current.Version != request.Version)
    {
        return Results.Conflict(new { error = $"Schema version is stale. Current version is {current.Version}." });
    }

    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new { error = "Schema name is required." });
    }

    if (request.Fields is null || request.Fields.Count == 0)
    {
        return Results.BadRequest(new { error = "At least one field is required." });
    }

    var fieldPaths = request.Fields.Select(f => f.Path).ToList();
    if (fieldPaths.Any(string.IsNullOrWhiteSpace))
    {
        return Results.BadRequest(new { error = "Field path is required for all fields." });
    }

    if (fieldPaths.Distinct(StringComparer.Ordinal).Count() != fieldPaths.Count)
    {
        return Results.BadRequest(new { error = "Duplicate field paths are not allowed." });
    }

    if (!request.IsActiveSource && current.IsActiveSource)
    {
        var all = await registry.ListAsync(cancellationToken);
        var otherActive = all.Any(s => !string.Equals(s.Id, id, StringComparison.Ordinal) && s.IsActiveSource);
        if (!otherActive)
        {
            return Results.BadRequest(new { error = "At least one schema must remain the active source." });
        }
    }

    var fields = request.Fields.Select(f => new SchemaField(
        f.Path, f.DataType, f.IsSensitive, f.RequiresApproval, f.DataOwnerRoles ?? [], f.Description)).ToList();

    var updated = current with
    {
        Name = request.Name.Trim(),
        DataSource = request.DataSource,
        Database = request.Database.Trim(),
        Collection = request.Collection.Trim(),
        Fields = fields,
        IsActiveSource = request.IsActiveSource,
        Description = request.Description,
        UpdatedAt = DateTimeOffset.UtcNow,
        UpdatedBy = SessionClaims.ToRequesterContext(user).UserId
    };

    var saved = await registry.SaveAsync(updated, cancellationToken);
    if (request.IsActiveSource)
    {
        await registry.ActivateAsync(saved.Id, cancellationToken);
        saved = await registry.GetAsync(saved.Id, cancellationToken) ?? saved;
    }

    await audit.AppendAsync(BuildSchemaAuditDoc(user, request.DataSource, "update", saved.Id, saved.Version), cancellationToken);
    await activeSchema.RefreshAsync(cancellationToken);
    return Results.Ok(new SchemaResponse(saved));
}).RequireAuthorization();

app.MapPost("/api/schemas/{id}/fields", async (
    string id,
    SchemaFieldRequest request,
    ClaimsPrincipal user,
    ISchemaRegistry registry,
    IAuditLogStore audit,
    ActiveSchemaProvider activeSchema,
    CancellationToken cancellationToken) =>
{
    if (!IsDataOwnerOrAdmin(user))
    {
        return Results.Forbid();
    }

    var current = await registry.GetAsync(id, cancellationToken);
    if (current is null)
    {
        return Results.NotFound();
    }

    if (string.IsNullOrWhiteSpace(request.Path))
    {
        return Results.BadRequest(new { error = "Field path is required." });
    }

    if (current.Fields.Any(f => string.Equals(f.Path, request.Path, StringComparison.Ordinal)))
    {
        return Results.Conflict(new { error = $"Field '{request.Path}' already exists." });
    }

    var newField = new SchemaField(
        request.Path, request.DataType, request.IsSensitive, request.RequiresApproval,
        request.DataOwnerRoles ?? [], request.Description);

    var updated = current with
    {
        Fields = current.Fields.Append(newField).ToList(),
        UpdatedAt = DateTimeOffset.UtcNow,
        UpdatedBy = SessionClaims.ToRequesterContext(user).UserId
    };

    var saved = await registry.SaveAsync(updated, cancellationToken);
    await audit.AppendAsync(BuildSchemaAuditDoc(user, saved.DataSource, "add-field", saved.Id, saved.Version, request.Path), cancellationToken);
    await activeSchema.RefreshAsync(cancellationToken);
    return Results.Ok(new SchemaResponse(saved));
}).RequireAuthorization();

app.MapPut("/api/schemas/{id}/fields/{path}", async (
    string id,
    string path,
    SchemaFieldRequest request,
    ClaimsPrincipal user,
    ISchemaRegistry registry,
    IAuditLogStore audit,
    ActiveSchemaProvider activeSchema,
    CancellationToken cancellationToken) =>
{
    if (!IsDataOwnerOrAdmin(user))
    {
        return Results.Forbid();
    }

    var current = await registry.GetAsync(id, cancellationToken);
    if (current is null)
    {
        return Results.NotFound();
    }

    var fieldsList = current.Fields.ToList();
    var existingIndex = fieldsList.FindIndex(f => string.Equals(f.Path, path, StringComparison.Ordinal));
    if (existingIndex < 0)
    {
        return Results.NotFound(new { error = $"Field '{path}' not found." });
    }

    var updatedField = new SchemaField(
        request.Path, request.DataType, request.IsSensitive, request.RequiresApproval,
        request.DataOwnerRoles ?? [], request.Description);

    fieldsList[existingIndex] = updatedField;

    var updated = current with
    {
        Fields = fieldsList,
        UpdatedAt = DateTimeOffset.UtcNow,
        UpdatedBy = SessionClaims.ToRequesterContext(user).UserId
    };

    var saved = await registry.SaveAsync(updated, cancellationToken);
    await audit.AppendAsync(BuildSchemaAuditDoc(user, saved.DataSource, "update-field", saved.Id, saved.Version, path), cancellationToken);
    await activeSchema.RefreshAsync(cancellationToken);
    return Results.Ok(new SchemaResponse(saved));
}).RequireAuthorization();

app.MapDelete("/api/schemas/{id}/fields/{path}", async (
    string id,
    string path,
    ClaimsPrincipal user,
    ISchemaRegistry registry,
    IAuditLogStore audit,
    ActiveSchemaProvider activeSchema,
    CancellationToken cancellationToken) =>
{
    if (!IsDataOwnerOrAdmin(user))
    {
        return Results.Forbid();
    }

    var current = await registry.GetAsync(id, cancellationToken);
    if (current is null)
    {
        return Results.NotFound();
    }

    if (!current.Fields.Any(f => string.Equals(f.Path, path, StringComparison.Ordinal)))
    {
        return Results.NotFound(new { error = $"Field '{path}' not found." });
    }

    var updated = current with
    {
        Fields = current.Fields.Where(f => !string.Equals(f.Path, path, StringComparison.Ordinal)).ToList(),
        UpdatedAt = DateTimeOffset.UtcNow,
        UpdatedBy = SessionClaims.ToRequesterContext(user).UserId
    };

    var saved = await registry.SaveAsync(updated, cancellationToken);
    await audit.AppendAsync(BuildSchemaAuditDoc(user, saved.DataSource, "delete-field", saved.Id, saved.Version, path), cancellationToken);
    await activeSchema.RefreshAsync(cancellationToken);
    return Results.Ok(new SchemaResponse(saved));
}).RequireAuthorization();

app.MapPost("/api/schemas/{id}/activate", async (
    string id,
    ClaimsPrincipal user,
    ISchemaRegistry registry,
    IAuditLogStore audit,
    ActiveSchemaProvider activeSchema,
    CancellationToken cancellationToken) =>
{
    if (!IsDataOwnerOrAdmin(user))
    {
        return Results.Forbid();
    }

    var current = await registry.GetAsync(id, cancellationToken);
    if (current is null)
    {
        return Results.NotFound();
    }

    await registry.ActivateAsync(id, cancellationToken);
    var activated = await registry.GetAsync(id, cancellationToken);
    await audit.AppendAsync(BuildSchemaAuditDoc(user, activated?.DataSource ?? current.DataSource, "activate", id, activated?.Version ?? current.Version), cancellationToken);
    await activeSchema.RefreshAsync(cancellationToken);
    return Results.Ok(new { id, isActiveSource = true });
}).RequireAuthorization();

app.MapPost("/api/schemas/import", async (
    ImportSchemaRequest request,
    ClaimsPrincipal user,
    ISchemaRegistry registry,
    IAuditLogStore audit,
    ActiveSchemaProvider activeSchema,
    CancellationToken cancellationToken) =>
{
    if (!IsDataOwnerOrAdmin(user))
    {
        return Results.Forbid();
    }

    IReadOnlyList<SchemaFieldRequest> fields;
    SchemaDataSource dataSource = SchemaDataSource.Mongo;

    if (!string.IsNullOrWhiteSpace(request.FieldsJson))
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter<SchemaFieldType>() }
            };
            fields = System.Text.Json.JsonSerializer.Deserialize<List<SchemaFieldRequest>>(request.FieldsJson, options) ?? [];
        }
        catch
        {
            return Results.BadRequest(new { error = "Invalid FieldsJson payload." });
        }
    }
    else if (!string.IsNullOrWhiteSpace(request.SchemaTxt))
    {
        fields = ParseSchemaTxt(request.SchemaTxt);
    }
    else
    {
        return Results.BadRequest(new { error = "Either FieldsJson or SchemaTxt is required." });
    }

    if (fields.Count == 0)
    {
        return Results.BadRequest(new { error = "No fields to import." });
    }

    var makeActive = (await registry.ListAsync(cancellationToken)).All(s => !s.IsActiveSource);

    var schema = new SchemaDefinition(
        SchemaDefinition.NewId(),
        "Imported Schema",
        dataSource,
        "sample_airbnb",
        "listingsAndReviews",
        fields.Select(f => new SchemaField(
            f.Path, f.DataType, f.IsSensitive, f.RequiresApproval, f.DataOwnerRoles ?? [], f.Description)).ToList(),
        makeActive,
        1,
        "Imported via /api/schemas/import",
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow,
        SessionClaims.ToRequesterContext(user).UserId);

    var created = await registry.CreateAsync(schema, cancellationToken);
    if (makeActive)
    {
        await registry.ActivateAsync(created.Id, cancellationToken);
        created = await registry.GetAsync(created.Id, cancellationToken) ?? created;
    }

    await audit.AppendAsync(BuildSchemaAuditDoc(user, dataSource, "import", created.Id, created.Version), cancellationToken);
    await activeSchema.RefreshAsync(cancellationToken);
    return Results.Ok(new SchemaResponse(created));
}).RequireAuthorization();

app.MapDelete("/api/schemas/{id}", async (
    string id,
    ClaimsPrincipal user,
    ISchemaRegistry registry,
    IAuditLogStore audit,
    ActiveSchemaProvider activeSchema,
    CancellationToken cancellationToken) =>
{
    if (!IsDataOwnerOrAdmin(user))
    {
        return Results.Forbid();
    }

    var current = await registry.GetAsync(id, cancellationToken);
    if (current is null)
    {
        return Results.NotFound();
    }

    if (current.IsActiveSource)
    {
        return Results.Conflict(new
        {
            error = "The active schema cannot be deleted. Activate another schema first."
        });
    }

    await registry.DeleteAsync(id, cancellationToken);
    await audit.AppendAsync(BuildSchemaAuditDoc(user, current.DataSource, "delete", id, current.Version), cancellationToken);
    await activeSchema.RefreshAsync(cancellationToken);
    return Results.NoContent();
}).RequireAuthorization();

#endregion

app.Run();

static IReadOnlyList<SchemaFieldRequest> ParseSchemaTxt(string schemaTxt)
{
    var fields = new List<SchemaFieldRequest>();
    foreach (var rawLine in schemaTxt.Split('\n', StringSplitOptions.RemoveEmptyEntries))
    {
        var line = rawLine.Trim();
        if (!line.StartsWith("- ", StringComparison.Ordinal))
        {
            continue;
        }

        var entry = line[2..];
        var colon = entry.IndexOf(':');
        if (colon <= 0)
        {
            continue;
        }

        var path = entry[..colon].Trim();
        var rest = entry[(colon + 1)..].Trim();
        var paren = rest.IndexOf('(');
        if (paren >= 0)
        {
            rest = rest[..paren].Trim();
        }

        var dataType = rest.ToLowerInvariant() switch
        {
            "string" => SchemaFieldType.String,
            "number" => SchemaFieldType.Number,
            "boolean" => SchemaFieldType.Boolean,
            "date" => SchemaFieldType.Date,
            "object" => SchemaFieldType.Object,
            _ => rest.StartsWith("array", StringComparison.OrdinalIgnoreCase) ? SchemaFieldType.Array : SchemaFieldType.String
        };

        if (path.Length > 0)
        {
            fields.Add(new SchemaFieldRequest(path, dataType, false, false, [], string.Empty));
        }
    }
    return fields;
}

static async Task WriteAgentEvent(HttpContext context, AgentEvent agentEvent, CancellationToken cancellationToken)
{
    var payload = JsonSerializer.Serialize(new { status = agentEvent.Status, detail = agentEvent.Detail });
    await context.Response.WriteAsync($"event: {agentEvent.Name}\n", cancellationToken);
    await context.Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
    await context.Response.Body.FlushAsync(cancellationToken);
}

public sealed record ApprovalFlagRequest(bool Enabled);

public sealed record GovernanceDecisionRequest(string? Notes);

public sealed record EmailBriefingRequest(string? Recipient);

public sealed record EnterpriseCoreQueryRequest(string? Query, string? Collection, IReadOnlyList<string>? Columns);

public partial class Program;
