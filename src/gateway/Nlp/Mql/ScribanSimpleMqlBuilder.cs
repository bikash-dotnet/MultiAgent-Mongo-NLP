using System.Text.Json;
using Gateway.Nlp.Slots;
using Scriban;

namespace Gateway.Nlp.Mql;

public sealed class ScribanSimpleMqlBuilder : IMqlBuilder
{
    private readonly Template _match;
    private readonly Template _sort;
    private readonly Template _limit;

    private ScribanSimpleMqlBuilder(string templatesDirectory)
    {
        _match = Template.Parse(File.ReadAllText(Path.Combine(templatesDirectory, "match.scriban")));
        _sort = Template.Parse(File.ReadAllText(Path.Combine(templatesDirectory, "sort.scriban")));
        _limit = Template.Parse(File.ReadAllText(Path.Combine(templatesDirectory, "limit.scriban")));
    }

    public static ScribanSimpleMqlBuilder FromAssetsDirectory(string templatesDirectory)
    {
        return new ScribanSimpleMqlBuilder(templatesDirectory);
    }

    public string Build(ExtractedSlots slots, MqlDefaults defaults)
    {
        var stages = new List<string>();
        var conditions = BuildConditions(slots);

        if (conditions.Count > 0)
        {
            stages.Add(_match.Render(new { has_conditions = true, conditions }).Trim());
        }

        var (sortField, sortDirection) = ResolveSort(defaults.Sort);
        stages.Add(_sort.Render(new { sort_field = sortField, sort_direction = sortDirection }).Trim());
        stages.Add(_limit.Render(new { limit = defaults.Limit }).Trim());

        return "[" + string.Join(",", stages) + "]";
    }

    private static List<string> BuildConditions(ExtractedSlots slots)
    {
        var conditions = new List<string>();

        if (slots.Market is not null)
        {
            conditions.Add($"\"address.market\": {JsonSerializer.Serialize(slots.Market)}");
        }

        var priceParts = new List<string>();
        if (slots.MinPrice is decimal min)
        {
            priceParts.Add($"\"$gte\": {JsonSerializer.Serialize(min)}");
        }

        if (slots.MaxPrice is decimal max)
        {
            priceParts.Add($"\"$lte\": {JsonSerializer.Serialize(max)}");
        }

        if (priceParts.Count > 0)
        {
            conditions.Add($"\"price\": {{ {string.Join(", ", priceParts)} }}");
        }

        if (slots.MinBeds is int beds)
        {
            conditions.Add($"\"beds\": {{ \"$gte\": {beds} }}");
        }

        if (slots.MinBedrooms is int bedrooms)
        {
            conditions.Add($"\"bedrooms\": {{ \"$gte\": {bedrooms} }}");
        }

        if (slots.Amenities.Count > 0)
        {
            var all = string.Join(",", slots.Amenities.Select(a => JsonSerializer.Serialize(a)));
            conditions.Add($"\"amenities\": {{ \"$all\": [{all}] }}");
        }

        return conditions;
    }

    private static (string Field, int Direction) ResolveSort(string sort)
    {
        return sort switch
        {
            "rating_desc" => ("review_scores.rating", -1),
            "price_asc" => ("price", 1),
            "price_desc" => ("price", -1),
            _ => ("review_scores.rating", -1)
        };
    }
}
