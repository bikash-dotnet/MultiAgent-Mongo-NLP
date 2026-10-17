using Microsoft.AspNetCore.Http;

namespace Gateway.Infrastructure.Filters;

public sealed class ValidationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        foreach (var argument in context.Arguments)
        {
            if (argument is null)
            {
                continue;
            }

            var validationResult = Validate(argument);
            if (validationResult is not null)
            {
                return validationResult;
            }
        }

        return await next(context);
    }

    private static IResult? Validate(object value)
    {
        var type = value.GetType();
        var properties = type.GetProperties();

        foreach (var prop in properties)
        {
            if (prop.PropertyType == typeof(string))
            {
                var strValue = prop.GetValue(value) as string;
                if (prop.Name.Equals("Name", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(strValue))
                {
                    return Microsoft.AspNetCore.Http.Results.BadRequest(new { error = "Name is required." });
                }
            }
        }

        return null;
    }
}
