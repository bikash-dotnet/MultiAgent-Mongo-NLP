using Gateway.Auth;
using Gateway.Execution;
using Gateway.Governance;
using Gateway.Hubs;
using Gateway.Nlp.Orchestrator;
using Microsoft.AspNetCore.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Gateway.Endpoints;

public static class DevEndpoints
{
    public static IEndpointRouteBuilder MapDevEndpoints(this IEndpointRouteBuilder app, string signingKey, string issuer, string audience)
    {
        var env = app.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        if (!env.IsDevelopment())
        {
            return app;
        }

        var dev = app.MapGroup("/dev");

        dev.MapGet("/token", (IConfiguration config) =>
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
            return Microsoft.AspNetCore.Http.Results.Ok(new { token = new JwtSecurityTokenHandler().WriteToken(jwt) });
        });

        dev.MapPost("/enterprise-core/query", (EnterpriseCoreQueryRequest body) =>
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
            return Microsoft.AspNetCore.Http.Results.Ok(new { columns = result.Columns, rows = result.Rows });
        });

        return app;
    }
}
