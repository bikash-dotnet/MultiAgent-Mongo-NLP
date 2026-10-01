using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Gateway.Execution;

public sealed record EnterpriseCoreResponse(IReadOnlyList<string> Columns, IReadOnlyList<Dictionary<string, string?>> Rows);

public sealed class EnterpriseCoreQueryTransport : IQueryTransport
{
    private readonly HttpClient _http;
    private readonly EnterpriseCoreOptions _options;

    public EnterpriseCoreQueryTransport(HttpClient http, IOptions<EnterpriseCoreOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await _http.PostAsJsonAsync(
                _options.QueryPath,
                new { query = request.Mql, collection = request.TargetCollection, columns = request.Columns },
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<EnterpriseCoreResponse>(cancellationToken);
            if (payload is null)
            {
                return new TabularResult([], [], ExecutionDataSource.EnterpriseCoreREST.ToString(), stopwatch.ElapsedMilliseconds, Error: "Enterprise Core returned an empty response.");
            }

            var rows = payload.Rows
                .Select(row => (IReadOnlyDictionary<string, string?>)row)
                .ToList();

            return new TabularResult(
                payload.Columns,
                rows,
                ExecutionDataSource.EnterpriseCoreREST.ToString(),
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return new TabularResult([], [], ExecutionDataSource.EnterpriseCoreREST.ToString(), stopwatch.ElapsedMilliseconds, Error: error.Message);
        }
    }
}
