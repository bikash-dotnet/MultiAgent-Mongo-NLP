using Gateway.Auth;
using Gateway.Endpoints;
using Gateway.Execution;
using Gateway.Governance;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Gateway.Handlers;

public static class DevHandlers
{
    public static IResult GetToken(IConfiguration config, string signingKey, string issuer, string audience)
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
    }

    public static IResult PostEnterpriseCoreQuery(EnterpriseCoreQueryRequest body)
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
    }
}
