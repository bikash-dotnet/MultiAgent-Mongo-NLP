namespace Gateway.Nlp.Llm;

public sealed class NvidiaNimOptions
{
    public const string SectionName = "NvidiaNim";

    public string BaseUrl { get; set; } = "https://integrate.api.nvidia.com/v1";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
    public int MaxAttempts { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 30;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.Equals(ApiKey, "TBD", StringComparison.OrdinalIgnoreCase);
}
