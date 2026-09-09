using Gateway.Nlp.Slots;

namespace Gateway.Tests.Nlp;

public class SlotExtractorTests
{
    private static readonly Gazetteer Gazetteer = Gazetteer.LoadFromDirectory(
        Path.Combine(AppContext.BaseDirectory, "Nlp", "Assets", "Gazetteers"));

    [Fact]
    public void Extracts_market_from_city_name()
    {
        var slots = SlotExtractor.Extract("listings in Los Angeles", Gazetteer);
        Assert.Equal("Los Angeles", slots.Market);
    }

    [Fact]
    public void Extracts_market_from_alias()
    {
        var slots = SlotExtractor.Extract("places in LA", Gazetteer);
        Assert.Equal("Los Angeles", slots.Market);
    }

    [Fact]
    public void Extracts_price_under_bound()
    {
        var slots = SlotExtractor.Extract("homes under $200", Gazetteer);
        Assert.Equal(200m, slots.MaxPrice);
        Assert.Null(slots.MinPrice);
    }

    [Fact]
    public void Extracts_price_range()
    {
        var slots = SlotExtractor.Extract("homes between 150 and 250", Gazetteer);
        Assert.Equal(150m, slots.MinPrice);
        Assert.Equal(250m, slots.MaxPrice);
    }

    [Fact]
    public void Extracts_bedrooms()
    {
        var slots = SlotExtractor.Extract("2 bedroom apartments", Gazetteer);
        Assert.Equal(2, slots.MinBedrooms);
    }

    [Fact]
    public void Extracts_amenity_via_alias()
    {
        var slots = SlotExtractor.Extract("apartments with pools", Gazetteer);
        Assert.Contains("Pool", slots.Amenities);
    }

    [Fact]
    public void Detects_just_run_it()
    {
        var slots = SlotExtractor.Extract("listings with pools in Los Angeles, just run it", Gazetteer);
        Assert.True(slots.JustRunIt);
        Assert.Equal("Los Angeles", slots.Market);
        Assert.Contains("Pool", slots.Amenities);
    }

    [Fact]
    public void Returns_empty_for_generic_browse()
    {
        var slots = SlotExtractor.Extract("show me top listings", Gazetteer);
        Assert.Null(slots.Market);
        Assert.Null(slots.MaxPrice);
        Assert.Empty(slots.Amenities);
        Assert.False(slots.JustRunIt);
    }
}
