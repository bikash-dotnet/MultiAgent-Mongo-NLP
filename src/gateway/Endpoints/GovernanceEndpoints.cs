using Gateway.Auth;
using Gateway.Governance;
using System.Security.Claims;

namespace Gateway.Endpoints;

public static class GovernanceEndpoints
{
    public static IEndpointRouteBuilder MapGovernanceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/governance/approval", (IApprovalFlagStore flags) =>
            Microsoft.AspNetCore.Http.Results.Ok(new { enabled = flags.Enabled })).RequireAuthorization();

        app.MapPut("/api/governance/approval", async (
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
        }).RequireAuthorization();

        return app;
    }
}
