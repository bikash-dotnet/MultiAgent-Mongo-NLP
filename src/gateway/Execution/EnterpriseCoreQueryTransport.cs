using System.Text.Json;
using System.Diagnostics;
using Gateway.Configuration;

namespace Gateway.Execution;

public sealed record EnterpriseCoreResponse(IReadOnlyList<string> Columns, IReadOnlyList<Dictionary<string, string?>> Rows);

public sealed class EnterpriseCoreQueryTransport : IQueryTransport
{
    private readonly HttpClient _http;
    private readonly ISettingsProvider _settings;

    public EnterpriseCoreQueryTransport(HttpClient http, ISettingsProvider settings)
    {
        _http = http;
        _settings = settings;
    }

    public async Task<TabularResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var baseUrl = ResolveBaseUrl();
            var queryPath = ResolveQueryPath();
            var url = string.IsNullOrWhiteSpace(queryPath) ? baseUrl : baseUrl.TrimEnd('/') + "/" + queryPath.TrimStart('/');

            using var response = await _http.PostAsJsonAsync(
                url,
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

    private string ResolveBaseUrl()
    {
        if (_settings.TryGetValue("EnterpriseCore", "BaseUrl", out var value) &&
            value.ValueKind == JsonValueKind.String)
        {
            var url = value.GetString();
            if (!string.IsNullOrWhiteSpace(url))
            {
                return url;
            }
        }
        return string.Empty;
    }

    private string ResolveQueryPath()
    {
        if (_settings.TryGetValue("EnterpriseCore", "QueryPath", out var value) &&
            value.ValueKind == JsonValueKind.String)
        {
            var path = value.GetString();
            if (!string.IsNullOrWhiteSpace(path))
            {
                return path;
            }
        }
        return "/api/query";
    }
}
