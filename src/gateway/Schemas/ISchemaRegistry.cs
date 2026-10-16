using Gateway.Schemas;

namespace Gateway.Schemas;

public interface ISchemaRegistry
{
    Task<IReadOnlyList<SchemaDefinition>> ListAsync(CancellationToken cancellationToken = default);

    Task<SchemaDefinition?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<SchemaDefinition?> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<SchemaDefinition> CreateAsync(SchemaDefinition schema, CancellationToken cancellationToken = default);

    Task<SchemaDefinition> SaveAsync(SchemaDefinition schema, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task SeedAsync(CancellationToken cancellationToken = default);
}
