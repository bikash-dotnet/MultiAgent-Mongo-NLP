using System.Linq;
using System.Text.Json;
using Gateway.Audit;
using Gateway.Conversations;
using Gateway.Nlp;
using Gateway.Schemas;
using System.Collections.Immutable;

namespace Gateway.Rules;

public sealed class ColumnDefinitionService
{
    private readonly IColumnDefinitionStore _store;
    private readonly IColumnCatalog _catalog;
    private readonly ActiveSchemaProvider? _schema;
    private readonly IAuditLogStore? _audit;

    public ColumnDefinitionService(
        IColumnDefinitionStore store,
        IColumnCatalog catalog,
        IAuditLogStore? audit = null,
        ActiveSchemaProvider? schema = null)
    {
        _store = store;
        _catalog = catalog;
        _audit = audit;
        _schema = schema;
    }

    public async Task<IReadOnlyList<ColumnDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _store.ListAsync(cancellationToken);
        var storedByField = stored.ToDictionary(d => d.Field, StringComparer.OrdinalIgnoreCase);

        var knownFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ColumnDefinition>();

        // Stored definitions first, preserving their order.
        foreach (var def in stored.OrderBy(d => d.Order))
        {
            knownFields.Add(def.Field);
            result.Add(def);
        }

        // Fallback defaults for schema/standard fields without a stored definition.
        var orderOffset = result.Count;
        var index = 0;
        foreach (var field in _catalog.Available(null).Select(d => d.Name))
        {
            if (knownFields.Contains(field))
            {
                continue;
            }
            knownFields.Add(field);
            result.Add(new ColumnDefinition(
                ColumnDefinition.NewId(),
                field,
                field,
                ColumnDataType.String,
                null,
                orderOffset + index++,
                true,
                true,
                false,
                null,
                0, null, null));
        }

        return result.OrderBy(d => d.Order).ThenBy(d => d.Field, StringComparer.Ordinal).ToList();
    }

    public async Task<ColumnDefinition> CreateAsync(ColumnDefinitionRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        var existing = (await _store.ListAsync(cancellationToken))
            .FirstOrDefault(d => string.Equals(d.Field, request.Field, StringComparison.Ordinal));
        if (existing is not null)
        {
            throw new InvalidOperationException($"Column definition for field '{request.Field}' already exists.");
        }

        ValidateDataType(request.DataType, request.Format);

        var maxOrder = (await _store.ListAsync(cancellationToken))
            .Select(d => d.Order)
            .DefaultIfEmpty(0)
            .Max();
        var def = new ColumnDefinition(
            ColumnDefinition.NewId(),
            request.Field,
            request.Label,
            request.DataType,
            request.Format,
            maxOrder + 1,
            request.VisibleByDefault,
            request.Exportable,
            request.Sensitive,
            request.Width,
            1, DateTimeOffset.UtcNow, updatedBy);

        var stored = await _store.UpsertAsync(def, updatedBy, cancellationToken);

        await AuditColumnDefinitionAsync(updatedBy, "column-definitions", "column_definition.create", stored.Id, stored.Version,
            $"created column '{stored.Field}' (type={stored.DataType})", cancellationToken);

        return stored;
    }

    public async Task<ColumnDefinition> UpdateAsync(
        string id,
        ColumnDefinitionRequest request,
        string updatedBy,
        int version,
        CancellationToken cancellationToken = default)
    {
        var current = await _store.GetAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Column definition '{id}' not found.");

        if (current.Version != version)
        {
            throw new InvalidOperationException($"Column definition '{id}' has been modified by another client.");
        }

        ValidateDataType(request.DataType, request.Format);

        var def = current with
        {
            Field = request.Field,
            Label = request.Label,
            DataType = request.DataType,
            Format = request.Format,
            Order = request.Order,
            VisibleByDefault = request.VisibleByDefault,
            Exportable = request.Exportable,
            Sensitive = request.Sensitive,
            Width = request.Width,
            Version = current.Version + 1,
            UpdatedAt = DateTimeOffset.UtcNow,
            UpdatedBy = updatedBy
        };

        var stored = await _store.UpsertAsync(def, updatedBy, cancellationToken);

        await AuditColumnDefinitionAsync(updatedBy, "column-definitions", "column_definition.update", stored.Id, stored.Version,
            $"updated column '{stored.Field}' (type={stored.DataType})", cancellationToken);

        return stored;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var current = await _store.GetAsync(id, cancellationToken);
        if (current is null)
        {
            return false;
        }

        var deleted = await _store.DeleteAsync(id, cancellationToken);

        if (deleted)
        {
            await AuditColumnDefinitionAsync("anonymous", "column-definitions", "column_definition.delete", id, current.Version,
                $"deleted column '{current.Field}' (type={current.DataType})", cancellationToken);
        }

        return deleted;
    }

    public async Task ReorderAsync(ColumnDefinitionReorderRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        var all = await _store.ListAsync(cancellationToken);
        var byId = all.ToDictionary(d => d.Id, StringComparer.Ordinal);

        var order = 0;
        foreach (var id in request.Ids)
        {
            if (byId.TryGetValue(id, out var def))
            {
                var updated = def with { Order = order++, Version = def.Version + 1, UpdatedAt = DateTimeOffset.UtcNow, UpdatedBy = updatedBy };
                await _store.UpsertAsync(updated, updatedBy, cancellationToken);
            }
        }

        await AuditColumnDefinitionAsync(updatedBy, "column-definitions", "column_definition.reorder", null, null,
            $"reordered {request.Ids.Count} columns", cancellationToken);
    }

    private async Task AuditColumnDefinitionAsync(
        string updatedBy,
        string dataSource,
        string action,
        string? entityId,
        int? version,
        string notes,
        CancellationToken cancellationToken)
    {
        if (_audit is null)
        {
            return;
        }

        await _audit.AppendAsync(AuditLogDocument.ForSchemaManagement(
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow,
            updatedBy,
            SchemaDataSource.Mongo,
            action,
            version ?? 0,
            notes), cancellationToken);
    }

    private static void ValidateDataType(ColumnDataType dataType, string? format)
    {
        if (dataType == ColumnDataType.Currency && string.IsNullOrWhiteSpace(format))
        {
            throw new InvalidOperationException("Currency data type requires a format (e.g. currency code).");
        }
        if (dataType == ColumnDataType.Date && string.IsNullOrWhiteSpace(format))
        {
            throw new InvalidOperationException("Date data type requires a format (e.g. date pattern).");
        }
    }
}
