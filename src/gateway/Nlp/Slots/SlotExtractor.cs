using System.Text.RegularExpressions;

namespace Gateway.Nlp.Slots;

public static class SlotExtractor
{
    private static readonly Regex PriceUnder = new(@"under\s+\$?\s*(\d+(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PriceOver = new(@"over\s+\$?\s*(\d+(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PriceRange = new(@"\$?\s*(\d+(?:\.\d+)?)\s*[-–]\s*\$?\s*(\d+(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PriceBetween = new(@"between\s+\$?\s*(\d+(?:\.\d+)?)\s+and\s+\$?\s*(\d+(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Beds = new(@"(\d+)\s*(bedroom|bed)s?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static ExtractedSlots Extract(string utterance, Gazetteer gazetteer)
    {
        var lower = utterance.ToLowerInvariant();
        var amenities = gazetteer.AmenityAliases
            .Where(kvp => lower.Contains(kvp.Key))
            .Select(kvp => kvp.Value)
            .Distinct()
            .ToList();

        string? market = null;
        var marketCandidates = gazetteer.MarketAliases
            .Where(kvp => lower.Contains(kvp.Key))
            .OrderByDescending(kvp => kvp.Key.Length)
            .ToList();
        if (marketCandidates.Count > 0)
        {
            market = marketCandidates[0].Value;
        }

        var between = PriceBetween.Match(lower);
        var range = PriceRange.Match(lower);
        var under = PriceUnder.Match(lower);
        var over = PriceOver.Match(lower);
        var beds = Beds.Match(lower);

        decimal? minPrice = null, maxPrice = null;
        int? bedrooms = null, minBeds = null;

        if (between.Success)
        {
            minPrice = ParsePrice(between.Groups[1].Value);
            maxPrice = ParsePrice(between.Groups[2].Value);
        }
        else if (range.Success)
        {
            minPrice = ParsePrice(range.Groups[1].Value);
            maxPrice = ParsePrice(range.Groups[2].Value);
        }
        else if (under.Success)
        {
            maxPrice = ParsePrice(under.Groups[1].Value);
        }
        else if (over.Success)
        {
            minPrice = ParsePrice(over.Groups[1].Value);
        }

        if (beds.Success)
        {
            var value = int.Parse(beds.Groups[1].Value);
            if (beds.Groups[2].Value.Contains("bedroom", StringComparison.OrdinalIgnoreCase))
            {
                bedrooms = value;
            }
            else
            {
                minBeds = value;
            }
        }

        var justRunIt = lower.Contains("just run it") || lower.Contains("go ahead") || lower.Contains("run it");

        return new ExtractedSlots(market, bedrooms, minBeds, minPrice, maxPrice, amenities, justRunIt);
    }

    private static decimal ParsePrice(string raw)
    {
        return decimal.TryParse(raw, out var value) ? value : 0m;
    }
}
