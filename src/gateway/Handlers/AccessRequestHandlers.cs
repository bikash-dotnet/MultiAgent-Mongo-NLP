using Gateway.Auth;
using Gateway.Endpoints;
using Gateway.Governance;
using System.Security.Claims;

namespace Gateway.Handlers;

public static class AccessRequestHandlers
{
    public static async Task<IResult> List(
        string? status,
        ClaimsPrincipal user,
        IGovernanceService governance,
        CancellationToken cancellationToken)
    {
        var viewer = SessionClaims.ToRequesterContext(user);
        var requestsList = await governance.ListAsync(viewer, status, cancellationToken);
        return Results.Ok(requestsList);
    }

    public static async Task<IResult> Get(
        string id,
        ClaimsPrincipal user,
        IGovernanceService governance,
        CancellationToken cancellationToken)
    {
        var viewer = SessionClaims.ToRequesterContext(user);
        try
        {
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
    }

    public static async Task<IResult> Approve(
        string id,
        GovernanceDecisionRequest? body,
        ClaimsPrincipal user,
        IGovernanceService governance,
        CancellationToken cancellationToken)
    {
        var actor = SessionClaims.ToRequesterContext(user);
        try
        {
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
    }

    public static async Task<IResult> Reject(
        string id,
        GovernanceDecisionRequest? body,
        ClaimsPrincipal user,
        IGovernanceService governance,
        CancellationToken cancellationToken)
    {
        var actor = SessionClaims.ToRequesterContext(user);
        try
        {
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
    }

    public static async Task<IResult> Override(
        string id,
        GovernanceDecisionRequest? body,
        ClaimsPrincipal user,
        IGovernanceService governance,
        CancellationToken cancellationToken)
    {
        var actor = SessionClaims.ToRequesterContext(user);
        try
        {
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
    }
}
