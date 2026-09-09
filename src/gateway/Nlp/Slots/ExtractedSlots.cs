namespace Gateway.Nlp.Slots;

public sealed record ExtractedSlots(
    string? Market,
    int? MinBedrooms,
    int? MinBeds,
    decimal? MinPrice,
    decimal? MaxPrice,
    IReadOnlyList<string> Amenities,
    bool JustRunIt)
{
    public static readonly ExtractedSlots Empty = new(null, null, null, null, null, Array.Empty<string>(), false);

    public bool HasAnyConstraints => Market is not null || MinBedrooms is not null || MinBeds is not null ||
                                     MinPrice is not null || MaxPrice is not null || Amenities.Count > 0;
}
