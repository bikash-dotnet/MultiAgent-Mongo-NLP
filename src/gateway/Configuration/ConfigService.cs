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

        var current = await _store.GetAsync(sectionName, cancellationToken);
        if (current is not null && current.Version != request.Version)
        {
            throw new OptimisticConcurrencyException(sectionName);
        }

        var now = DateTimeOffset.UtcNow;
        var userId = user.FindFirst(SessionClaims.UserId)?.Value ?? "anonymous";

        await _store.UpsertAsync(sectionName, validatedJson, userId, cancellationToken);

        var root = JsonElement.Parse(validatedJson.ToString()).Clone();
        _provider.UpdateSection(sectionName, root, current is null ? 1 : current.Version + 1);

        await AuditChangeAsync(userId, sectionName, request.Values, now, cancellationToken);

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
        await AuditChangeAsync(userId, sectionName, new Dictionary<string, JsonElement>(), DateTimeOffset.UtcNow, cancellationToken);
    }

    public async Task ResetAllAsync(CancellationToken cancellationToken = default)
    {
        await _store.ResetAsync(cancellationToken);
        _provider.ResetAll();
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
                    var shortKey = spec.Key[(spec.Key.IndexOf('.') + 1)..];
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
            var shortKey = spec.Key[(spec.Key.IndexOf('.') + 1)..];

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

        // Cross-field rule: EnterpriseCore.BaseUrl required when Execution.DataSource = EnterpriseCoreREST
        if (string.Equals(sectionName, "Execution", StringComparison.Ordinal) &&
            inputByKey.TryGetValue("DataSource", out var ds) &&
            ds.ValueKind == JsonValueKind.String &&
            ds.GetString() == "EnterpriseCoreREST")
        {
            if (!dict.TryGetValue("BaseUrl", out var baseUrl) ||
                baseUrl.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(baseUrl.GetString()))
            {
                throw new InvalidOperationException(
                    "EnterpriseCore.BaseUrl is required when Execution.DataSource is EnterpriseCoreREST.");
            }
        }

        // Range validation for Number/Port/Double settings.
        foreach (var spec in section.Settings)
        {
            var shortKey = spec.Key[(spec.Key.IndexOf('.') + 1)..];
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
        IReadOnlyDictionary<string, JsonElement> input,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        var doc = new AuditLogDocument(
            Guid.NewGuid().ToString("N"),
            timestamp,
            "config-management",
            sectionName,
            new AuditLogUser(userId, userId, null, string.Empty),
            new AuditLogNlpPerformance(false, false, 0, 0),
            new AuditLogRequestDetails("", System.Collections.Immutable.ImmutableDictionary<string, string>.Empty),
            new AuditLogExecutionDetails("", "app_settings", 0, null, null),
            new AuditLogGovernance(false, [], null, false, $"{userId}: updated config section {sectionName}"),
            "config.update",
            0);

        await _audit.AppendAsync(doc, cancellationToken);
    }
}
