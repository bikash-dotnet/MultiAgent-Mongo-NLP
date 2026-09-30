using Microsoft.Extensions.Options;

namespace Gateway.Execution;

public sealed class RoutingQueryExecutor : IQueryTransport
{
    private readonly IQueryTransport _mongo;
    private readonly IQueryTransport _rest;
    private readonly ExecutionOptions _options;

    public RoutingQueryExecutor(IQueryTransport mongo, IQueryTransport rest, IOptions<ExecutionOptions> options)
    {
        _mongo = mongo;
        _rest = rest;
        _options = options.Value;
    }

    public Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var source = request.DataSource == default ? _options.DataSource : request.DataSource;
        return source == ExecutionDataSource.EnterpriseCoreREST
            ? _rest.ExecuteAsync(request, cancellationToken)
            : _mongo.ExecuteAsync(request, cancellationToken);
    }
}
