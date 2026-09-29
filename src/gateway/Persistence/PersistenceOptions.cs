namespace Gateway.Persistence;

public sealed class PersistenceOptions
{
    public const string SectionName = "Persistence";

    public string Mode { get; set; } = "auto";

    public string DataDirectory { get; set; } = ".data";
}
