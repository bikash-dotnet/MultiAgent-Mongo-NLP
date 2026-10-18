using System.Text.Json;
using System.Text.Json.Serialization;
using Gateway.Audit;
using Gateway.Infrastructure.Results;
using Gateway.Nlp;
using Gateway.Schemas;

namespace Gateway.Services;

public sealed class SchemaService : ISchemaService
{
    private const string Collection = "schemas";

    private readonly ISchemaRegistry _registry;
    private readonly IAuditLogStore _audit;
    private readonly ActiveSchemaProvider _activeSchema;

    public SchemaService(
        ISchemaRegistry registry,
        IAuditLogStore audit,
        ActiveSchemaProvider activeSchema)
    {
        _registry = registry;
        _audit = audit;
        _activeSchema = activeSchema;
    }

    public async Task<ApiResult<SchemaListResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        var schemas = await _registry.ListAsync(cancellationToken);
        var summaries = schemas.Select(s => new SchemaSummary(
            s.Id, s.Name, s.DataSource, s.Fields.Count, s.IsActiveSource, s.Version, s.UpdatedAt, s.UpdatedBy));
        return ApiResult<SchemaListResponse>.Ok(new SchemaListResponse(summaries.ToList()));
    }

    public async Task<ApiResult<SchemaResponse>> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var schema = await _registry.GetAsync(id, cancellationToken);
        return schema is null
            ? ApiResult<SchemaResponse>.Error(StatusCodes.Status404NotFound, $"Schema '{id}' was not found.")
            : ApiResult<SchemaResponse>.Ok(new SchemaResponse(schema));
    }

    public async Task<ApiResult<SchemaResponse>> CreateAsync(
        CreateSchemaRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ApiResult<SchemaResponse>.Error(StatusCodes.Status400BadRequest, "Schema name is required.");
        }

        if (request.Fields is null || request.Fields.Count == 0)
        {
            return ApiResult<SchemaResponse>.Error(StatusCodes.Status400BadRequest, "At least one field is required.");
        }

        var fieldPaths = request.Fields.Select(f => f.Path).ToList();
        if (fieldPaths.Any(string.IsNullOrWhiteSpace))
        {
            return ApiResult<SchemaResponse>.Error(StatusCodes.Status400BadRequest, "Field path is required for all fields.");
        }

        if (fieldPaths.Distinct(StringComparer.Ordinal).Count() != fieldPaths.Count)
        {
            return ApiResult<SchemaResponse>.Error(StatusCodes.Status400BadRequest, "Duplicate field paths are not allowed.");
        }

        var fields = request.Fields.Select(f => new SchemaField(
            f.Path, f.DataType, f.IsSensitive, f.RequiresApproval, f.DataOwnerRoles ?? [], f.Description)).ToList();

        var existing = await _registry.ListAsync(cancellationToken);
        var makeActive = request.IsActiveSource || existing.All(s => !s.IsActiveSource);

        var schema = new SchemaDefinition(
            SchemaDefinition.NewId(),
            request.Name.Trim(),
            request.DataSource,
            request.Database.Trim(),
            request.Collection.Trim(),
            fields,
            makeActive,
            1,
            request.Description,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            userId);

        var created = await _registry.CreateAsync(schema, cancellationToken);
        if (makeActive)
        {
            await _registry.ActivateAsync(created.Id, cancellationToken);
            created = await _registry.GetAsync(created.Id, cancellationToken) ?? created;
        }

        await _audit.AppendAsync(BuildSchemaAuditDoc(userId, request.DataSource, "create", created.Id, created.Version), cancellationToken);
        await _activeSchema.RefreshAsync(cancellationToken);
        return ApiResult<SchemaResponse>.Ok(new SchemaResponse(created));
    }

    public async Task<ApiResult<SchemaResponse>> UpdateAsync(
        string id,
        UpdateSchemaRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var current = await _registry.GetAsync(id, cancellationToken);
        if (current is null)
        {
            return ApiResult<SchemaResponse>.Error(StatusCodes.Status404NotFound, $"Schema '{id}' was not found.");
        }

        if (current.Version != request.Version)
        {
            return ApiResult<SchemaResponse>.Error(StatusCodes.Status409Conflict, $"Schema version is stale. Current version is {current.Version}.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ApiResult<SchemaResponse>.Error(StatusCodes.Status400BadRequest, "Schema name is required.");
        }

        if (request.Fields is null || request.Fields.Count == 0)
        {
            return ApiResult<SchemaResponse>.Error(StatusCodes.Status400BadRequest, "At least one field is required.");
        }

        var fieldPaths = request.Fields.Select(f => f.Path).ToList();
        if (fieldPaths.Any(string.IsNullOrWhiteSpace))
        {
            return ApiResult<SchemaResponse>.Error(StatusCodes.Status400BadRequest, "Field path is required for all fields.");
        }

        if (fieldPaths.Distinct(StringComparer.Ordinal).Count() != fieldPaths.Count)
        {
            return ApiResult<SchemaResponse>.Error(StatusCodes.Status400BadRequest, "Duplicate field paths are not allowed.");
        }

        if (!request.IsActiveSource && current.IsActiveSource)
        {
            var all = await _registry.ListAsync(cancellationToken);
            var otherActive = all.Any(s => !string.Equals(s.Id, id, StringComparison.Ordinal) && s.IsActiveSource);
            if (!otherActive)
            {
                return ApiResult<SchemaResponse>.Error(StatusCodes.Status400BadRequest, "At least one schema must remain the active source.");
            }
        }

        var fields = request.Fields.Select(f => new SchemaField(
            f.Path, f.DataType, f.IsSensitive, f.RequiresApproval, f.DataOwnerRoles ?? [], f.Description)).ToList();

        var updated = current with
        {
            Name = request.Name.Trim(),
            DataSource = request.DataSource,
            Database = request.Database.Trim(),
            Collection = request.Collection.Trim(),
            Fields = fields,
            IsActiveSource = request.IsActiveSource,
            Description = request.Description,
            UpdatedAt = DateTimeOffset.UtcNow,
            UpdatedBy = userId
        };

        var saved = await _registry.SaveAsync(updated, cancellationToken);
        if (request.IsActiveSource)
        {
            await _registry.ActivateAsync(saved.Id, cancellationToken);
            saved = await _registry.GetAsync(saved.Id, cancellationToken) ?? saved;
        }

        await _audit.AppendAsync(BuildSchemaAuditDoc(userId, request.DataSource, "update", saved.Id, saved.Version), cancellationToken);
        await _activeSchema.RefreshAsync(cancellationToken);
        return ApiResult<SchemaResponse>.Ok(new SchemaResponse(saved));
    }

    public async Task<ApiResult<SchemaResponse>> AddFieldAsync(
        string id,
        SchemaFieldRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var current = await _registry.GetAsync(id, cancellationToken);
        if (current is null)
        {
            return ApiResult<SchemaResponse>.Error(StatusCodes.Status404NotFound, $"Schema '{id}' was not found.");
        }

        if (string.IsNullOrWhiteSpace(request.Path))
        {
            return ApiResult<SchemaResponse>.Error(StatusCodes.Status400BadRequest, "Field path is required.");
        }

        if (current.Fields.Any(f => string.Equals(f.Path, request.Path, StringComparison.Ordinal)))
        {
            return ApiResult<SchemaResponse>.Error(StatusCodes.Status409Conflict, $"Field '{request.Path}' already exists.");
        }

        var newField = new SchemaField(
            request.Path, request.DataType, request.IsSensitive, request.RequiresApproval,
            request.DataOwnerRoles ?? [], request.Description);

        var updated = current with
        {
            Fields = current.Fields.Append(newField).ToList(),
            UpdatedAt = DateTimeOffset.UtcNow,
            UpdatedBy = userId
        };

        var saved = await _registry.SaveAsync(updated, cancellationToken);
        await _audit.AppendAsync(BuildSchemaAuditDoc(userId, saved.DataSource, "add-field", saved.Id, saved.Version, request.Path), cancellationToken);
        await _activeSchema.RefreshAsync(cancellationToken);
        return ApiResult<SchemaResponse>.Ok(new SchemaResponse(saved));
    }

    public async Task<ApiResult<SchemaResponse>> UpdateFieldAsync(
        string id,
        string path,
        SchemaFieldRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var current = await _registry.GetAsync(id, cancellationToken);
        if (current is null)
        {
            return ApiResult<SchemaResponse>.Error(StatusCodes.Status404NotFound, $"Schema '{id}' was not found.");
        }

        var fieldsList = current.Fields.ToList();
        var existingIndex = fieldsList.FindIndex(f => string.Equals(f.Path, path, StringComparison.Ordinal));
        if (existingIndex < 0)
        {
            return ApiResult<SchemaResponse>.Error(StatusCodes.Status404NotFound, $"Field '{path}' not found.");
        }

        if (string.IsNullOrWhiteSpace(request.Path))
        {
            return ApiResult<SchemaResponse>.Error(StatusCodes.Status400BadRequest, "Field path is required.");
        }

        var renamed = !string.Equals(request.Path, path, StringComparison.Ordinal);
        if (renamed && current.Fields.Any(f => string.Equals(f.Path, request.Path, StringComparison.Ordinal)))
        {
            return ApiResult<SchemaResponse>.Error(StatusCodes.Status409Conflict, $"Field '{request.Path}' already exists.");
        }

        var updatedField = new SchemaField(
            request.Path, request.DataType, request.IsSensitive, request.RequiresApproval,
            request.DataOwnerRoles ?? [], request.Description);

        fieldsList[existingIndex] = updatedField;

        var updated = current with
        {
            Fields = fieldsList,
            UpdatedAt = DateTimeOffset.UtcNow,
            UpdatedBy = userId
        };

        var saved = await _registry.SaveAsync(updated, cancellationToken);
        await _audit.AppendAsync(BuildSchemaAuditDoc(userId, saved.DataSource, "update-field", saved.Id, saved.Version, path), cancellationToken);
        await _activeSchema.RefreshAsync(cancellationToken);
        return ApiResult<SchemaResponse>.Ok(new SchemaResponse(saved));
    }

    public async Task<ApiResult<SchemaResponse>> DeleteFieldAsync(
        string id,
        string path,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var current = await _registry.GetAsync(id, cancellationToken);
        if (current is null)
        {
            return ApiResult<SchemaResponse>.Error(StatusCodes.Status404NotFound, $"Schema '{id}' was not found.");
        }

        if (!current.Fields.Any(f => string.Equals(f.Path, path, StringComparison.Ordinal)))
        {
            return ApiResult<SchemaResponse>.Error(StatusCodes.Status404NotFound, $"Field '{path}' not found.");
        }

        var updated = current with
        {
            Fields = current.Fields.Where(f => !string.Equals(f.Path, path, StringComparison.Ordinal)).ToList(),
            UpdatedAt = DateTimeOffset.UtcNow,
            UpdatedBy = userId
        };

        var saved = await _registry.SaveAsync(updated, cancellationToken);
        await _audit.AppendAsync(BuildSchemaAuditDoc(userId, saved.DataSource, "delete-field", saved.Id, saved.Version, path), cancellationToken);
        await _activeSchema.RefreshAsync(cancellationToken);
        return ApiResult<SchemaResponse>.Ok(new SchemaResponse(saved));
    }

    public async Task<ApiResult<SchemaResponse>> ActivateAsync(
        string id,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var current = await _registry.GetAsync(id, cancellationToken);
        if (current is null)
        {
            return ApiResult<SchemaResponse>.Error(StatusCodes.Status404NotFound, $"Schema '{id}' was not found.");
        }

        await _registry.ActivateAsync(id, cancellationToken);
        var activated = await _registry.GetAsync(id, cancellationToken);
        await _audit.AppendAsync(BuildSchemaAuditDoc(userId, activated?.DataSource ?? current.DataSource, "activate", id, activated?.Version ?? current.Version), cancellationToken);
        await _activeSchema.RefreshAsync(cancellationToken);
        return ApiResult<SchemaResponse>.Ok(new SchemaResponse(activated ?? current));
    }

    public async Task<ApiResult<SchemaResponse>> ImportAsync(
        ImportSchemaRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SchemaFieldRequest> fields;
        var dataSource = SchemaDataSource.Mongo;

        if (!string.IsNullOrWhiteSpace(request.FieldsJson))
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter<SchemaFieldType>() }
                };
                fields = JsonSerializer.Deserialize<List<SchemaFieldRequest>>(request.FieldsJson, options) ?? [];
            }
            catch
            {
                return ApiResult<SchemaResponse>.Error(StatusCodes.Status400BadRequest, "Invalid FieldsJson payload.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.SchemaTxt))
        {
            fields = ParseSchemaTxt(request.SchemaTxt);
        }
        else
        {
            return ApiResult<SchemaResponse>.Error(StatusCodes.Status400BadRequest, "Either FieldsJson or SchemaTxt is required.");
        }

        if (fields.Count == 0)
        {
            return ApiResult<SchemaResponse>.Error(StatusCodes.Status400BadRequest, "No fields to import.");
        }

        var importPaths = fields.Select(f => f.Path).ToList();
        if (importPaths.Any(string.IsNullOrWhiteSpace))
        {
            return ApiResult<SchemaResponse>.Error(StatusCodes.Status400BadRequest, "Field path is required for all imported fields.");
        }

        if (importPaths.Distinct(StringComparer.Ordinal).Count() != importPaths.Count)
        {
            return ApiResult<SchemaResponse>.Error(StatusCodes.Status400BadRequest, "Duplicate field paths are not allowed in the import payload.");
        }

        var makeActive = (await _registry.ListAsync(cancellationToken)).All(s => !s.IsActiveSource);

        var schema = new SchemaDefinition(
            SchemaDefinition.NewId(),
            "Imported Schema",
            dataSource,
            "sample_airbnb",
            "listingsAndReviews",
            fields.Select(f => new SchemaField(
                f.Path, f.DataType, f.IsSensitive, f.RequiresApproval, f.DataOwnerRoles ?? [], f.Description)).ToList(),
            makeActive,
            1,
            "Imported via /api/schemas/import",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            userId);

        var created = await _registry.CreateAsync(schema, cancellationToken);
        if (makeActive)
        {
            await _registry.ActivateAsync(created.Id, cancellationToken);
            created = await _registry.GetAsync(created.Id, cancellationToken) ?? created;
        }

        await _audit.AppendAsync(BuildSchemaAuditDoc(userId, dataSource, "import", created.Id, created.Version), cancellationToken);
        await _activeSchema.RefreshAsync(cancellationToken);
        return ApiResult<SchemaResponse>.Ok(new SchemaResponse(created));
    }

    public async Task<ApiResult<SchemaResponse>> DeleteAsync(
        string id,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var current = await _registry.GetAsync(id, cancellationToken);
        if (current is null)
        {
            return ApiResult<SchemaResponse>.Error(StatusCodes.Status404NotFound, $"Schema '{id}' was not found.");
        }

        if (current.IsActiveSource)
        {
            return ApiResult<SchemaResponse>.Error(StatusCodes.Status409Conflict, "The active schema cannot be deleted. Activate another schema first.");
        }

        await _registry.DeleteAsync(id, cancellationToken);
        await _audit.AppendAsync(BuildSchemaAuditDoc(userId, current.DataSource, "delete", id, current.Version), cancellationToken);
        await _activeSchema.RefreshAsync(cancellationToken);
        return ApiResult<SchemaResponse>.NoContent();
    }

    private static AuditLogDocument BuildSchemaAuditDoc(
        string userId,
        SchemaDataSource dataSource,
        string action,
        string? schemaId,
        int version,
        string? notes = null)
    {
        var viewer = new AuditLogUser(userId, userId, null, string.Empty);
        return new AuditLogDocument(
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow,
            "schema-management",
            dataSource.ToString(),
            viewer,
            new AuditLogNlpPerformance(false, false, 0, 0),
            new AuditLogRequestDetails("", System.Collections.Immutable.ImmutableDictionary<string, string>.Empty),
            new AuditLogExecutionDetails("", "schemas", 0, null, null),
            new AuditLogGovernance(false, [], null, false, $"{viewer.Name}: {action} {(schemaId ?? string.Empty)} v{version}" + (notes is not null ? $" ({notes})" : string.Empty)));
    }

    private static IReadOnlyList<SchemaFieldRequest> ParseSchemaTxt(string schemaTxt)
    {
        var fields = new List<SchemaFieldRequest>();
        foreach (var rawLine in schemaTxt.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("- ", StringComparison.Ordinal))
            {
                continue;
            }

            var entry = line[2..];
            var colon = entry.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var path = entry[..colon].Trim();
            var rest = entry[(colon + 1)..].Trim();
            var paren = rest.IndexOf('(');
            if (paren >= 0)
            {
                rest = rest[..paren].Trim();
            }

            var dataType = rest.ToLowerInvariant() switch
            {
                "string" => SchemaFieldType.String,
                "number" => SchemaFieldType.Number,
                "boolean" => SchemaFieldType.Boolean,
                "date" => SchemaFieldType.Date,
                "object" => SchemaFieldType.Object,
                _ => rest.StartsWith("array", StringComparison.OrdinalIgnoreCase) ? SchemaFieldType.Array : SchemaFieldType.String
            };

            if (path.Length > 0)
            {
                fields.Add(new SchemaFieldRequest(path, dataType, false, false, [], string.Empty));
            }
        }
        return fields;
    }
}
