using Gateway.Auth;
using Gateway.Endpoints;
using Gateway.Governance;
using System.Security.Claims;

namespace Gateway.Handlers;

public static class GovernanceHandlers
{
    public static IResult GetApproval(IApprovalFlagStore flags) =>
        Results.Ok(new { enabled = flags.Enabled });

    public static async Task<IResult> PutApproval(
        ApprovalFlagRequest request,
        ClaimsPrincipal user,
        IApprovalFlagStore flags,
        CancellationToken cancellationToken)
    {
        var role = user.FindFirst(SessionClaims.Role)?.Value;
        if (role != "Data Owner / Admin")
        {
            return Results.Forbid();
        }

        await flags.SetAsync(request.Enabled, cancellationToken);
        return Results.Ok(new { enabled = flags.Enabled });
    }
}
