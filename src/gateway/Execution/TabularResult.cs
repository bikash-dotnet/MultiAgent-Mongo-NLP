namespace Gateway.Execution;

public sealed record TabularResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows,
    string DataSource,
    long DurationMs,
    bool TimedOut = false,
    string? Error = null)
{
    public int RowCount => Rows.Count;

    public static TabularResult Empty(string dataSource)
    {
        return new TabularResult([], [], dataSource, 0);
    }
}
