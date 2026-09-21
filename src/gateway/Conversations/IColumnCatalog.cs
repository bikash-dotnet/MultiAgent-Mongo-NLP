namespace Gateway.Conversations;

public interface IColumnCatalog
{
    IReadOnlyList<ColumnOption> Available(string? mql);
}
