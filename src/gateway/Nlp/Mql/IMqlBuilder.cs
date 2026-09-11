using Gateway.Nlp.Slots;

namespace Gateway.Nlp.Mql;

public sealed record MqlDefaults(int Limit, string Sort, string Market)
{
    public static readonly MqlDefaults Standard = new(10, "rating_desc", "All");
}

public interface IMqlBuilder
{
    string Build(ExtractedSlots slots, MqlDefaults defaults);
}
