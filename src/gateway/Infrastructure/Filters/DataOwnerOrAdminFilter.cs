using Gateway.Auth;
using Microsoft.AspNetCore.Http;

namespace Gateway.Infrastructure.Filters;

public sealed class DataOwnerOrAdminFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var user = context.HttpContext.User;
        var role = user.FindFirst(SessionClaims.Role)?.Value;

        if (role != "Data Owner / Admin")
        {
            return Microsoft.AspNetCore.Http.Results.Forbid();
        }

        return await next(context);
    }
}
