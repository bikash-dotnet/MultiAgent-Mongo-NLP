using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Gateway.Audit;
using Gateway.Auth;

namespace Gateway.Configuration;

public sealed class ConfigService
{
    private readonly ISettingsStore _store;
    private readonly ISettingsProvider _provider;
    private readonly IAuditLogStore _audit;
    private readonly IConfiguration _configuration;

    public ConfigService(
        ISettingsStore store,
        ISettingsProvider provider,
        IAuditLogStore audit,
        IConfiguration configuration)
    {
        _store = store;
        _provider = provider;
        _audit = audit;
        _configuration = configuration;
    }

    public async Task<SectionSnapshot> GetSectionAsync(
        string sectionName,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        return await new SettingsService(_store, _configuration).GetSectionAsync(sectionName, cancellationToken);
    }

    public async Task<IReadOnlyList<SectionSnapshot>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await new SettingsService(_store, _configuration).GetAllAsync(cancellationToken);
    }

    public async Task<SectionSnapshot> UpdateSectionAsync(
        string sectionName,
        UpdateConfigSectionRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var section = SettingsCatalog.Sections.FirstOrDefault(s =>
            string.Equals(s.SectionName, sectionName, StringComparison.Ordinal));
        if (section is null)
        {
            throw new KeyNotFoundException($"Unknown configuration section '{sectionName}'.");
        }

        ValidateSection(sectionName, request.Values, out var validatedJson);

        await ValidateCrossFieldAsync(sectionName, request.Values, cancellationToken);

        var current = await _store.GetAsync(sectionName, cancellationToken);
        if (current is not null && current.Version != request.Version)
        {
            throw new OptimisticConcurrencyException(sectionName);
        }

        var now = DateTimeOffset.UtcNow;
        var userId = user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";

        var previous = current is null
            ? new Dictionary<string, JsonElement>()
            : current.ValuesAsElement().ValueKind == JsonValueKind.Object
                ? current.ValuesAsElement().EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone())
                : new Dictionary<string, JsonElement>();

        await _store.UpsertAsync(sectionName, validatedJson, userId, cancellationToken);

        var root = JsonElement.Parse(validatedJson.ToString()).Clone();
        _provider.UpdateSection(sectionName, root, current is null ? 1 : current.Version + 1);

        var next = validatedJson.ValueKind == JsonValueKind.Object
            ? validatedJson.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone())
            : new Dictionary<string, JsonElement>();

        await AuditChangeAsync(userId, sectionName, previous, next, now, cancellationToken);

        return await GetSectionAsync(sectionName, user, cancellationToken);
    }

    public async Task DeleteSectionAsync(string sectionName, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var section = SettingsCatalog.Sections.FirstOrDefault(s =>
            string.Equals(s.SectionName, sectionName, StringComparison.Ordinal));
        if (section is null)
        {
            throw new KeyNotFoundException($"Unknown configuration section '{sectionName}'.");
        }

        var current = await _store.GetAsync(sectionName, cancellationToken);
        if (current is null)
        {
            return;
        }

        await _store.DeleteAsync(sectionName, cancellationToken);
        _provider.RemoveSection(sectionName);

        var userId = user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";
        await AuditChangeAsync(userId, sectionName, new Dictionary<string, JsonElement>(), new Dictionary<string, JsonElement>(), DateTimeOffset.UtcNow, cancellationToken, deleted: true);
    }

    public async Task ResetAllAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var userId = "system";

        await _store.ResetAsync(cancellationToken);
        _provider.ResetAll();

        await AuditChangeAsync(userId, "all", new Dictionary<string, JsonElement>(), new Dictionary<string, JsonElement>(), now, cancellationToken, deleted: true);
    }

    public ConfigSchemaResponse GetSchema()
    {
        return new ConfigSchemaResponse(
            SettingsCatalog.Sections.Select(section => new SettingsSectionSchema(
                section.SectionName,
                section.Label,
                section.AdminOnly,
                section.Settings.Select(spec =>
                {
                    var shortKey = ShortKey(spec.Key);
                    return new SettingsFieldSchema(
                        shortKey,
                        spec.Label,
                        spec.Type.ToString(),
                        spec.Editable,
                        spec.Secret,
                        spec.Default.ValueKind == JsonValueKind.Null || spec.Default.ValueKind == JsonValueKind.Undefined
                            ? null
                            : spec.Default,
                        spec.Min,
                        spec.Max,
                        spec.MinD,
                        spec.MaxD,
                        spec.AllowedValues,
                        spec.Description);
                }).ToList())).ToList());
    }

    private void ValidateSection(
        string sectionName,
        IReadOnlyDictionary<string, JsonElement> input,
        out JsonElement validated)
    {
        var section = SettingsCatalog.Sections.First(s =>
            string.Equals(s.SectionName, sectionName, StringComparison.Ordinal));
        var dict = new Dictionary<string, JsonElement>();

        var inputByKey = input.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

        foreach (var spec in section.Settings)
        {
            var shortKey = ShortKey(spec.Key);

            if (!spec.Editable && input.ContainsKey(shortKey))
            {
                throw new InvalidOperationException($"Setting '{shortKey}' in section '{sectionName}' is read-only.");
            }

            if (spec.Secret)
            {
                continue;
            }

            if (!inputByKey.TryGetValue(shortKey, out var raw))
            {
                raw = spec.Default.ValueKind == JsonValueKind.Null || spec.Default.ValueKind == JsonValueKind.Undefined
                    ? JsonSerializer.SerializeToElement((string?)null)
                    : spec.Default;
            }

            dict[shortKey] = Coerce(raw, spec);
        }

        // Range validation for Number/Port/Double settings.
        foreach (var spec in section.Settings)
        {
            var shortKey = ShortKey(spec.Key);
            if (!dict.TryGetValue(shortKey, out var value) || value.ValueKind != JsonValueKind.Number)
            {
                continue;
            }

            if ((spec.Type is SettingType.Number or SettingType.Port) && spec.Min is int min && spec.Max is int max)
            {
                var ivalue = value.GetInt32();
                if (ivalue < min || ivalue > max)
                {
                    throw new InvalidOperationException(
                        $"Setting '{shortKey}' must be between {min} and {max}.");
                }
            }
            else if (spec.Type == SettingType.Double && spec.MinD is double dmin && spec.MaxD is double dmax)
            {
                var dvalue = value.GetDouble();
                if (dvalue < dmin || dvalue > dmax)
                {
                    throw new InvalidOperationException(
                        $"Setting '{shortKey}' must be between {dmin} and {dmax}.");
                }
            }
        }

        validated = JsonSerializer.SerializeToElement(dict);
    }

    private static string ShortKey(string fullKey)
    {
        var dot = fullKey.LastIndexOf('.');
        return dot < 0 ? fullKey : fullKey[(dot + 1)..];
    }

    private async Task ValidateCrossFieldAsync(
        string sectionName,
        IReadOnlyDictionary<string, JsonElement> input,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(sectionName, "Execution", StringComparison.Ordinal))
        {
            return;
        }

        var inputByKey = input.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
        if (!inputByKey.TryGetValue("DataSource", out var ds) ||
            ds.ValueKind != JsonValueKind.String ||
            ds.GetString() != "EnterpriseCoreREST")
        {
            return;
        }

        var doc = await _store.GetAsync("EnterpriseCore", cancellationToken);
        if (doc is not null &&
            doc.ValuesAsElement().ValueKind == JsonValueKind.Object &&
            doc.ValuesAsElement().TryGetProperty("BaseUrl", out var dbBaseUrl) &&
            dbBaseUrl.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(dbBaseUrl.GetString()))
        {
            return;
        }

        throw new InvalidOperationException(
            "EnterpriseCore.BaseUrl is required when Execution.DataSource is EnterpriseCoreREST.");
    }

    private static JsonElement Coerce(JsonElement value, SettingSpec spec)
    {
        return spec.Type switch
        {
            SettingType.Number or SettingType.Port =>
                JsonSerializer.SerializeToElement(value.GetInt32()),
            SettingType.Double =>
                JsonSerializer.SerializeToElement(value.GetDouble()),
            SettingType.Boolean =>
                JsonSerializer.SerializeToElement(value.GetBoolean()),
            _ => value
        };
    }

    private async Task AuditChangeAsync(
        string userId,
        string sectionName,
        IReadOnlyDictionary<string, JsonElement> previous,
        IReadOnlyDictionary<string, JsonElement> next,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken,
        bool deleted = false)
    {
        var changedKeys = previous.Keys
            .Union(next.Keys)
            .Where(k => !EqualsValue(previous.GetValueOrDefault(k), next.GetValueOrDefault(k)))
            .ToList();

        var previousNonSecret = MaskSecrets(sectionName, previous);
        var nextNonSecret = MaskSecrets(sectionName, next);

        var doc = new AuditLogDocument(
            Guid.NewGuid().ToString("N"),
            timestamp,
            "config-management",
            sectionName,
            new AuditLogUser(userId, userId, null, string.Empty),
            new AuditLogNlpPerformance(false, false, 0, 0),
            new AuditLogRequestDetails("", System.Collections.Immutable.ImmutableDictionary<string, string>.Empty),
            new AuditLogExecutionDetails("", "app_settings", next.Count, null, null),
            new AuditLogGovernance(false, [], null, false,
                deleted
                    ? $"{userId}: deleted config section {sectionName}"
                    : $"{userId}: updated config section {sectionName}; changed: {string.Join(", ", changedKeys)}"),
            deleted ? "config.delete" : "config.update",
            next.Count);

        await _audit.AppendAsync(doc, cancellationToken);
    }

    private static bool EqualsValue(JsonElement? a, JsonElement? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return a.Value.ValueKind == b.Value.ValueKind && a.Value.GetRawText() == b.Value.GetRawText();
    }

    private static IReadOnlyDictionary<string, JsonElement> MaskSecrets(
        string sectionName,
        IReadOnlyDictionary<string, JsonElement> values)
    {
        var result = new Dictionary<string, JsonElement>();
        foreach (var kvp in values)
        {
            if (SettingsCatalog.IsSecret(sectionName, kvp.Key))
            {
                result[kvp.Key] = JsonSerializer.SerializeToElement((string?)null);
            }
            else
            {
                result[kvp.Key] = kvp.Value.Clone();
            }
        }
        return result;
    }
}
