using Gateway.Auth;
using Gateway.Governance;
using System.Security.Claims;

namespace Gateway.Endpoints;

public static class GovernanceEndpoints
{
    public static IEndpointRouteBuilder MapGovernanceEndpoints(this IEndpointRouteBuilder app)
    {
        var governance = app.MapGroup("/api/governance")
            .RequireAuthorization();

        governance.MapGet("/approval", (IApprovalFlagStore flags) =>
            Microsoft.AspNetCore.Http.Results.Ok(new { enabled = flags.Enabled }));

        governance.MapPut("/approval", async (
            ApprovalFlagRequest request,
            ClaimsPrincipal user,
            IApprovalFlagStore flags,
            CancellationToken cancellationToken) =>
        {
            var role = user.FindFirst(SessionClaims.Role)?.Value;
            if (role != "Data Owner / Admin")
            {
                return Microsoft.AspNetCore.Http.Results.Forbid();
            }

            await flags.SetAsync(request.Enabled, cancellationToken);
            return Microsoft.AspNetCore.Http.Results.Ok(new { enabled = flags.Enabled });
        });

        return app;
    }
}
