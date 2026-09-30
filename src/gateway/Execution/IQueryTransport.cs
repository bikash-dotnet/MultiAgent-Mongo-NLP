namespace Gateway.Execution;

public interface IQueryTransport
{
    Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default);
}
