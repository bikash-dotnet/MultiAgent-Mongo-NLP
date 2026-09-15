using Gateway.Nlp.Llm;

namespace Gateway.Tests.Nlp;

public class PipelineValidatorTests
{
    private readonly IPipelineValidator _validator = new PipelineValidator();

    [Theory]
    [InlineData("""[{"$group":{"_id":"$address.market","averagePrice":{"$avg":"$price"}}}]""")]
    [InlineData("""[{"$match":{"price":{"$lte":200}}},{"$limit":10}]""")]
    [InlineData("""[{"$match":{"amenities":{"$all":["Pool"]}}}]""")]
    public void Accepts_valid_pipelines(string pipeline)
    {
        Assert.True(_validator.Validate(pipeline).IsValid);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"$match":{}}""")]
    [InlineData("""[{"$out":"other"}]""")]
    [InlineData("""[{"$match":{"price":"cheap"}}]""")]
    [InlineData("""[{"$match":{"amenities":"Pool"}}]""")]
    public void Rejects_invalid_pipelines(string pipeline)
    {
        var result = _validator.Validate(pipeline);

        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }
}
