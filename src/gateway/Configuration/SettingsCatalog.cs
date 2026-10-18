using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gateway.Configuration;

[JsonConverter(typeof(JsonStringEnumConverter<SettingType>))]
public enum SettingType
{
    String,
    Number,
    Double,
    Boolean,
    Select,
    Url,
    Password,
    Port,
}

public sealed record SettingSpec(
    string Key,
    string Label,
    SettingType Type,
    bool Editable,
    bool Secret,
    JsonElement Default,
    int? Min = null,
    int? Max = null,
    double? MinD = null,
    double? MaxD = null,
    IReadOnlyList<string>? AllowedValues = null,
    string? Description = null);

public sealed record SettingsSectionSpec(
    string SectionName,
    string Label,
    IReadOnlyList<SettingSpec> Settings,
    bool AdminOnly = false);

public static class SettingsCatalog
{
    private static JsonElement Json(string json) => JsonElement.Parse(json).Clone();

    private static readonly IReadOnlyList<SettingSpec> ExecutionSettings = new List<SettingSpec>
    {
        new("Execution.DataSource", "Data source", SettingType.Select, true, false,
            Json("\"Mongo\""), AllowedValues: ["Mongo", "EnterpriseCoreREST"],
            Description: "Primary query execution source."),
        new("Execution.TimeoutMs", "Execution timeout (ms)", SettingType.Number, true, false,
            Json("5000"), Min: 1000, Max: 60000,
            Description: "Maximum query execution time before timeout."),
        new("Execution.Collection", "Collection", SettingType.String, true, false,
            Json("\"listingsAndReviews\""),
            Description: "Target collection for query execution."),
        new("Execution.Database", "Database", SettingType.String, true, false,
            Json("\"sample_airbnb\""),
            Description: "Target database for query execution.")
    };

    private static readonly IReadOnlyList<SettingSpec> NlpCacheSettings = new List<SettingSpec>
    {
        new("Nlp.Cache.Threshold", "Semantic cache threshold", SettingType.Double, true, false,
            Json("0.95"), MinD: 0.0, MaxD: 1.0,
            Description: "Cosine similarity threshold for semantic cache hits.")
    };

    private static readonly IReadOnlyList<SettingSpec> NlpDefaultsSettings = new List<SettingSpec>
    {
        new("Nlp.Defaults.Limit", "Default result limit", SettingType.Number, true, false,
            Json("10"), Min: 1, Max: 1000,
            Description: "Default number of rows returned per query."),
        new("Nlp.Defaults.Sort", "Default sort", SettingType.Select, true, false,
            Json("\"rating_desc\""), AllowedValues: ["rating_desc", "price_desc", "name_asc"],
            Description: "Default sort order for results."),
        new("Nlp.Defaults.Market", "Default market", SettingType.Select, true, false,
            Json("\"All\""), AllowedValues: ["All", "US", "EU", "APAC"],
            Description: "Default market filter applied to results.")
    };

    private static readonly IReadOnlyList<SettingSpec> NlpEmbeddingsSettings = new List<SettingSpec>
    {
        new("Nlp.Embeddings.MaxTokens", "Embeddings max tokens", SettingType.Number, true, false,
            Json("512"), Min: 64, Max: 4096,
            Description: "Maximum token count for embedding input.")
    };

    private static readonly IReadOnlyList<SettingSpec> GovernanceSettings = new List<SettingSpec>
    {
        new("Governance.ApprovalEnabled", "Approval required", SettingType.Boolean, true, false,
            Json("true"),
            Description: "Whether sensitive queries require data-owner approval.")
    };

    private static readonly IReadOnlyList<SettingSpec> ReportsSettings = new List<SettingSpec>
    {
        new("Reports.DemoFallbackEnabled", "Demo fallback enabled", SettingType.Boolean, true, false,
            Json("true"),
            Description: "Fallback to demo data when live sources are unavailable.")
    };

    private static readonly IReadOnlyList<SettingSpec> ReportsSmtpSettings = new List<SettingSpec>
    {
        new("Reports.Smtp.Host", "SMTP host", SettingType.String, true, false,
            Json("\"localhost\""),
            Description: "SMTP server host for report delivery."),
        new("Reports.Smtp.Port", "SMTP port", SettingType.Port, true, false,
            Json("25"), Min: 1, Max: 65535,
            Description: "SMTP server port."),
        new("Reports.Smtp.User", "SMTP user", SettingType.String, true, false,
            Json("\"\""),
            Description: "SMTP authentication user name."),
        new("Reports.Smtp.From", "SMTP from address", SettingType.String, true, false,
            Json("\"\""),
            Description: "From address used for report e-mail."),
        new("Reports.Smtp.Password", "SMTP password", SettingType.Password, false, true,
            Json("\"\""),
            Description: "SMTP authentication password. Managed by environment; never stored or returned.")
    };

    private static readonly IReadOnlyList<SettingSpec> EnterpriseCoreSettings = new List<SettingSpec>
    {
        new("EnterpriseCore.BaseUrl", "EnterpriseCore base URL", SettingType.Url, true, false,
            Json("\"\""),
            Description: "Base URL for the EnterpriseCore REST source."),
        new("EnterpriseCore.QueryPath", "EnterpriseCore query path", SettingType.String, true, false,
            Json("\"/api/query\""),
            Description: "Query path appended to the EnterpriseCore base URL.")
    };

    private static readonly IReadOnlyList<SettingSpec> NvidiaNimSettings = new List<SettingSpec>
    {
        new("NvidiaNim.Model", "NVIDIA NIM model", SettingType.String, true, false,
            Json("\"nvidia/nemotron-3-nano-omni-30b-a3b-reasoning\""),
            Description: "NVIDIA NIM model used for query generation."),
        new("NvidiaNim.BaseUrl", "NVIDIA NIM base URL", SettingType.Url, true, false,
            Json("\"https://integrate.api.nvidia.com/v1\""),
            Description: "Base URL for the NVIDIA NIM API."),
        new("NvidiaNim.MaxAttempts", "NVIDIA NIM max attempts", SettingType.Number, true, false,
            Json("3"), Min: 1, Max: 10,
            Description: "Maximum retry attempts for NVIDIA NIM calls."),
        new("NvidiaNim.TimeoutSeconds", "NVIDIA NIM timeout (s)", SettingType.Number, true, false,
            Json("30"), Min: 5, Max: 300,
            Description: "Timeout in seconds for NVIDIA NIM calls."),
        new("NvidiaNim.ApiKey", "NVIDIA NIM API key", SettingType.Password, false, true,
            Json("\"\""),
            Description: "NVIDIA NIM API key. Managed by environment; never stored or returned.")
    };

    private static readonly IReadOnlyList<SettingSpec> MongoDbSettings = new List<SettingSpec>
    {
        new("MongoDb.ConnectionString", "Connection string", SettingType.String, false, true,
            Json("\"\""),
            Description: "MongoDB connection string. Managed by environment; not editable."),
        new("MongoDb.Database", "Database", SettingType.String, false, false,
            Json("\"sample_airbnb\""),
            Description: "Target database. Read-only; set via environment.")
    };

    private static readonly IReadOnlyList<SettingSpec> JwtSettings = new List<SettingSpec>
    {
        new("Jwt.Issuer", "Issuer", SettingType.String, false, false,
            Json("\"MultiAgentMongoNlp\""),
            Description: "JWT issuer. Managed by environment; not editable."),
        new("Jwt.Audience", "Audience", SettingType.String, false, false,
            Json("\"MultiAgentMongoNlp.Spa\""),
            Description: "JWT audience. Managed by environment; not editable."),
        new("Jwt.SigningKey", "Signing key", SettingType.Password, false, true,
            Json("\"\""),
            Description: "JWT signing key. Managed by environment; never stored or returned.")
    };

    private static readonly IReadOnlyList<SettingSpec> PersistenceSettings = new List<SettingSpec>
    {
        new("Persistence.Mode", "Mode", SettingType.String, false, false,
            Json("\"Mongo\""),
            Description: "Persistence mode. Startup-bound; not editable."),
        new("Persistence.DataDirectory", "Data directory", SettingType.String, false, false,
            Json("\"data\""),
            Description: "Local data directory. Startup-bound; not editable.")
    };

    public static readonly IReadOnlyList<SettingsSectionSpec> Sections = new List<SettingsSectionSpec>
    {
        new("Execution", "Execution", ExecutionSettings),
        new("Nlp.Cache", "Semantic cache", NlpCacheSettings),
        new("Nlp.Defaults", "NLP defaults", NlpDefaultsSettings),
        new("Nlp.Embeddings", "Embeddings", NlpEmbeddingsSettings),
        new("Governance", "Governance", GovernanceSettings),
        new("Reports", "Reports", ReportsSettings),
        new("Reports.Smtp", "SMTP", ReportsSmtpSettings),
        new("EnterpriseCore", "EnterpriseCore", EnterpriseCoreSettings, AdminOnly: true),
        new("NvidiaNim", "NVIDIA NIM", NvidiaNimSettings),
        new("MongoDb", "MongoDB", MongoDbSettings, AdminOnly: true),
        new("Jwt", "JWT", JwtSettings, AdminOnly: true),
        new("Persistence", "Persistence", PersistenceSettings, AdminOnly: true)
    };

    public static SettingSpec? GetSpec(string sectionName, string key)
    {
        var fullKey = $"{sectionName}.{key}";
        foreach (var section in Sections)
        {
            var spec = section.Settings.FirstOrDefault(s => string.Equals(s.Key, fullKey, StringComparison.Ordinal));
            if (spec is not null)
            {
                return spec;
            }
        }
        return null;
    }

    public static JsonElement? DefaultValue(string sectionName, string key) =>
        GetSpec(sectionName, key)?.Default;

    public static bool IsEditable(string sectionName, string key) =>
        GetSpec(sectionName, key) is { Editable: true };

    public static bool IsSecret(string sectionName, string key) =>
        GetSpec(sectionName, key) is { Secret: true };

    public static bool IsSectionAdminOnly(string sectionName) =>
        Sections.FirstOrDefault(s => string.Equals(s.SectionName, sectionName, StringComparison.Ordinal))?.AdminOnly == true;
}
