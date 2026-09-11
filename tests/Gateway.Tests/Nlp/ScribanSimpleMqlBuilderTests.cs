using System.Text.Json;
using Gateway.Nlp.Mql;
using Gateway.Nlp.Slots;

namespace Gateway.Tests.Nlp;

public class ScribanSimpleMqlBuilderTests
{
    private static readonly IMqlBuilder Builder = ScribanSimpleMqlBuilder.FromAssetsDirectory(
        Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Templates"));

    private static JsonElement ParseArray(string json)
    {
        var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        return doc.RootElement.Clone();
    }

    [Fact]
    public void Defaults_only_produces_sort_and_limit()
    {
        var json = Builder.Build(ExtractedSlots.Empty, MqlDefaults.Standard);

        var stages = ParseArray(json);
        Assert.Equal(2, stages.GetArrayLength());
        Assert.Equal("$sort", stages[0].EnumerateObject().First().Name);
        Assert.Equal("$limit", stages[1].EnumerateObject().First().Name);
    }

    [Fact]
    public void Market_slot_produces_match_stage()
    {
        var slots = new ExtractedSlots("Los Angeles", null, null, null, null, new List<string>(), false);

        var json = Builder.Build(slots, MqlDefaults.Standard);

        var stages = ParseArray(json);
        Assert.Equal(3, stages.GetArrayLength());
        var match = stages[0];
        var field = match.GetProperty("$match");
        Assert.Equal("Los Angeles", field.GetProperty("address.market").GetString());
    }

    [Fact]
    public void Price_range_produces_gte_and_lte_merged()
    {
        var slots = new ExtractedSlots(null, null, null, 150m, 250m, new List<string>(), false);

        var json = Builder.Build(slots, MqlDefaults.Standard);

        var match = JsonDocument.Parse(json).RootElement[0].GetProperty("$match");
        var price = match.GetProperty("price");
        Assert.Equal(150, price.GetProperty("$gte").GetInt32());
        Assert.Equal(250, price.GetProperty("$lte").GetInt32());
    }

    [Fact]
    public void Amenities_produce_all_array()
    {
        var slots = new ExtractedSlots(null, null, null, null, null, new List<string> { "Pool", "Kitchen" }, false);

        var json = Builder.Build(slots, MqlDefaults.Standard);

        var match = JsonDocument.Parse(json).RootElement[0].GetProperty("$match");
        var amenities = match.GetProperty("amenities").GetProperty("$all");
        Assert.Equal("Pool", amenities[0].GetString());
        Assert.Equal("Kitchen", amenities[1].GetString());
    }

    [Fact]
    public void Bedrooms_produce_bedrooms_field()
    {
        var slots = new ExtractedSlots(null, 2, null, null, null, new List<string>(), false);

        var json = Builder.Build(slots, MqlDefaults.Standard);

        var match = JsonDocument.Parse(json).RootElement[0].GetProperty("$match");
        Assert.Equal(2, match.GetProperty("bedrooms").GetProperty("$gte").GetInt32());
    }

    [Fact]
    public void Sort_and_limit_reflect_defaults()
    {
        var json = Builder.Build(ExtractedSlots.Empty, MqlDefaults.Standard);

        var stages = ParseArray(json);
        Assert.Equal("review_scores.rating", stages[0].GetProperty("$sort").EnumerateObject().First().Name);
        Assert.Equal(-1, stages[0].GetProperty("$sort").EnumerateObject().First().Value.GetInt32());
        Assert.Equal(10, stages[1].GetProperty("$limit").GetInt32());
    }
}
