using Gateway.Nlp.Guardrails;

namespace Gateway.Tests.Nlp;

public class MqlAnalyzerTests
{
    [Fact]
    public void Extracts_fields_and_operators_from_match_sort_limit()
    {
        var analysis = MqlAnalyzer.Analyze(
            """[{"$match":{"price":{"$gte":50,"$lte":300},"address.market":"New York"}},{"$sort":{"review_scores.rating":-1}},{"$limit":10}]""");

        Assert.True(analysis.IsParsable);
        Assert.Contains("price", analysis.FieldPaths);
        Assert.Contains("address.market", analysis.FieldPaths);
        Assert.Contains("review_scores.rating", analysis.FieldPaths);
        Assert.Contains("$match", analysis.Operators);
        Assert.Contains("$gte", analysis.Operators);
        Assert.Contains("$lte", analysis.Operators);
        Assert.Contains("$sort", analysis.Operators);
        Assert.Contains("$limit", analysis.Operators);
    }

    [Fact]
    public void Extracts_nested_document_field_paths()
    {
        var analysis = MqlAnalyzer.Analyze("""[{"$match":{"address":{"location":{"market":"LA"}}}}]""");

        Assert.True(analysis.IsParsable);
        Assert.Contains("address", analysis.FieldPaths);
        Assert.Contains("address.location", analysis.FieldPaths);
        Assert.Contains("address.location.market", analysis.FieldPaths);
    }

    [Fact]
    public void Extracts_fields_from_logical_operators()
    {
        var analysis = MqlAnalyzer.Analyze(
            """[{"$match":{"$or":[{"amenities":{"$all":["Pool"]}},{"beds":{"$gte":3}}]}}]""");

        Assert.True(analysis.IsParsable);
        Assert.Contains("$or", analysis.Operators);
        Assert.Contains("amenities", analysis.FieldPaths);
        Assert.Contains("beds", analysis.FieldPaths);
    }

    [Fact]
    public void Extracts_group_key_and_accumulator_references()
    {
        var analysis = MqlAnalyzer.Analyze(
            """[{"$group":{"_id":"$address.market","averagePrice":{"$avg":"$price"},"total":{"$sum":1}}}]""");

        Assert.True(analysis.IsParsable);
        Assert.Contains("address.market", analysis.FieldPaths);
        Assert.Contains("price", analysis.FieldPaths);
        Assert.Contains("$group", analysis.Operators);
        Assert.Contains("$avg", analysis.Operators);
        Assert.Contains("$sum", analysis.Operators);
    }

    [Fact]
    public void Extracts_projection_and_unwind_paths()
    {
        var analysis = MqlAnalyzer.Analyze(
            """[{"$project":{"_id":0,"price":1,"city":"$address.market"}},{"$unwind":{"path":"$amenities"}}]""");

        Assert.True(analysis.IsParsable);
        Assert.Contains("price", analysis.FieldPaths);
        Assert.Contains("city", analysis.FieldPaths);
        Assert.Contains("address.market", analysis.FieldPaths);
        Assert.Contains("amenities", analysis.FieldPaths);
        Assert.Contains("$project", analysis.Operators);
        Assert.Contains("$unwind", analysis.Operators);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    public void Unparseable_pipeline_fails_closed(string pipeline)
    {
        var analysis = MqlAnalyzer.Analyze(pipeline);

        Assert.False(analysis.IsParsable);
        Assert.False(string.IsNullOrWhiteSpace(analysis.ParseError));
        Assert.Empty(analysis.FieldPaths);
    }

    [Fact]
    public void Non_array_root_is_not_parsable()
    {
        var analysis = MqlAnalyzer.Analyze("""{"$match":{}}""");

        Assert.False(analysis.IsParsable);
        Assert.Contains("array", analysis.ParseError);
    }
}
