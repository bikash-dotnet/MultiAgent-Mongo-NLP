using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Gateway.Auth;
using Gateway.Greeting;
using Gateway.Nlp;
using Gateway.Nlp.Http;
using Gateway.Nlp.Orchestrator;
using Gateway.Nlp.Router;
using Gateway.Observability;
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
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.Request.Path.StartsWithSegments("/api/agents/stream"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
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
    var result = await orchestrator.OrchestrateAsync(utterance, sessionId, cancellationToken);
    return Results.Ok(NlpQueryResponse.From(result));
}).RequireAuthorization();

app.MapGet("/api/agents/stream", async (HttpContext context, IAgentEventSink events, CancellationToken cancellationToken) =>
{
    context.Response.Headers.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers.Connection = "keep-alive";

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

app.Run();

static async Task WriteAgentEvent(HttpContext context, AgentEvent agentEvent, CancellationToken cancellationToken)
{
    var payload = JsonSerializer.Serialize(new { status = agentEvent.Status, detail = agentEvent.Detail });
    await context.Response.WriteAsync($"event: {agentEvent.Name}\n", cancellationToken);
    await context.Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
    await context.Response.Body.FlushAsync(cancellationToken);
}

public partial class Program;
