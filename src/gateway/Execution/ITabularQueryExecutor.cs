namespace Gateway.Execution;

public interface ITabularQueryExecutor
{
    Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default);
}
