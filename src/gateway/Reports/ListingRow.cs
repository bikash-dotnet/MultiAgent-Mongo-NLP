namespace Gateway.Reports;

public sealed record ListingRow(
    string Id,
    string Name,
    string Market,
    decimal Price,
    string RoomType,
    int Accommodates,
    double Rating,
    string? Latitude,
    string? Longitude);
