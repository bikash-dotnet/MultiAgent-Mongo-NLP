using Gateway.Auth;
using Gateway.Configuration;
using Gateway.Endpoints;
using Gateway.Nlp;
using Gateway.Observability;
using Gateway.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

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
builder.Services.AddScoped<ISchemaService, SchemaService>();

builder.Services.AddSingleton<ISettingsStore, SettingsStore>();
builder.Services.AddSingleton<ISettingsProvider, SettingsProvider>();
builder.Services.AddScoped<ConfigService>();

var app = builder.Build();

app.UseMiddleware<SessionCorrelationMiddleware>();
app.UseCors("spa");
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.MapDevEndpoints(signingKey, issuer, audience);
}

app.MapSessionEndpoints();
app.MapNlpEndpoints();
app.MapConversationEndpoints();
app.MapReportEndpoints();
app.MapAnalyticsEndpoints();
app.MapGovernanceEndpoints();
app.MapAccessRequestEndpoints();
app.MapAgentEndpoints();
app.MapSchemaEndpoints();
app.MapConfigEndpoints();

app.Run();

public partial class Program;
