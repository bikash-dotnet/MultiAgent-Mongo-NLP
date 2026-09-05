namespace Gateway.Observability;

public sealed class SessionCorrelationMiddleware
{
    public const string HeaderName = "X-Session-Id";
    private readonly RequestDelegate _next;
    private readonly ILogger<SessionCorrelationMiddleware> _logger;

    public SessionCorrelationMiddleware(RequestDelegate next, ILogger<SessionCorrelationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sessionId = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            sessionId = $"sess_{Guid.NewGuid():N}"[..16];
        }

        context.Items[HeaderName] = sessionId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = sessionId;
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object> { ["session_id"] = sessionId }))
        {
            await _next(context);
        }
    }
}
