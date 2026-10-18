namespace Gateway.Rules;

public interface IColumnDefinitionStore
{
    Task<ColumnDefinition?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ColumnDefinition>> ListAsync(CancellationToken cancellationToken = default);

    Task<ColumnDefinition> UpsertAsync(ColumnDefinition definition, string updatedBy,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
}
