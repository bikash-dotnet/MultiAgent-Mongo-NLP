using Gateway.Nlp.Guardrails;

namespace Gateway.Tests.Nlp;

public class ReadOnlyRuleTests
{
    [Theory]
    [InlineData("""[{"$out":"archive"}]""", "$out")]
    [InlineData("""[{"$merge":{"into":"archive"}}]""", "$merge")]
    public void Blocks_write_stages(string pipeline, string expected)
    {
        var violations = ReadOnlyRule.FindViolations(MqlAnalyzer.Analyze(pipeline));

        Assert.Contains(expected, violations);
    }

    [Fact]
    public void Allows_read_only_pipeline()
    {
        var analysis = MqlAnalyzer.Analyze(
            """[{"$match":{"price":{"$lte":200}}},{"$sort":{"price":1}},{"$limit":10}]""");

        Assert.Empty(ReadOnlyRule.FindViolations(analysis));
    }

    [Fact]
    public void Unparseable_pipeline_is_a_violation()
    {
        var violations = ReadOnlyRule.FindViolations(MqlAnalyzer.Analyze("not json"));

        Assert.Single(violations);
        Assert.False(string.IsNullOrWhiteSpace(violations[0]));
    }

    [Fact]
    public void Command_document_fails_closed()
    {
        var analysis = MqlAnalyzer.Analyze("""{"deleteMany":"listingsAndReviews"}""");

        Assert.False(analysis.IsParsable);
        Assert.NotEmpty(ReadOnlyRule.FindViolations(analysis));
    }
}
