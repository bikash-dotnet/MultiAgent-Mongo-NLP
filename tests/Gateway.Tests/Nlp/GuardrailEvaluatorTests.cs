using Gateway.Nlp.Guardrails;

namespace Gateway.Tests.Nlp;

public class GuardrailEvaluatorTests
{
    private static readonly GuardrailEvaluator Evaluator = new(
        SchemaWhitelist.FromFields(["price", "amenities", "address.market", "review_scores.rating"]),
        new InMemorySensitiveFieldRegistry(
        [
            new SensitiveFieldFlag("address.location.coordinates", true, true, ["DataOwner"]),
            new SensitiveFieldFlag("host.host_verifications", true, true, ["DataOwner"]),
            new SensitiveFieldFlag("host.host_identity_verified", true, false, ["DataOwner"])
        ]));

    [Fact]
    public void Clean_read_query_is_allowed()
    {
        var result = Evaluator.Evaluate(
            """[{"$match":{"price":{"$lte":200},"address.market":"New York"}},{"$limit":10}]""");

        Assert.Equal(GuardrailOutcome.Allowed, result.Outcome);
        Assert.Empty(result.SensitiveFields);
    }

    [Fact]
    public void Write_stage_is_rejected()
    {
        var result = Evaluator.Evaluate("""[{"$out":"archive"}]""");

        Assert.Equal(GuardrailOutcome.Rejected, result.Outcome);
        Assert.Contains("$out", result.BlockedOperators);
    }

    [Fact]
    public void Unparseable_pipeline_is_rejected()
    {
        var result = Evaluator.Evaluate("not json");

        Assert.Equal(GuardrailOutcome.Rejected, result.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    [Fact]
    public void Unknown_field_is_rejected()
    {
        var result = Evaluator.Evaluate("""[{"$match":{"secrets.token":"x"}}]""");

        Assert.Equal(GuardrailOutcome.Rejected, result.Outcome);
        Assert.Contains("secrets.token", result.UnknownFields);
    }

    [Fact]
    public void Sensitive_coordinates_pause_for_approval()
    {
        var result = Evaluator.Evaluate(
            """[{"$match":{"address.location.coordinates.lat":{"$gte":40}}}]""");

        Assert.Equal(GuardrailOutcome.PausedForApproval, result.Outcome);
        Assert.Contains("address.location.coordinates", result.SensitiveFields);
    }

    [Fact]
    public void Registry_known_field_not_in_schema_pauses()
    {
        var result = Evaluator.Evaluate("""[{"$match":{"host.host_verifications.0":"verified"}}]""");

        Assert.Equal(GuardrailOutcome.PausedForApproval, result.Outcome);
        Assert.Contains("host.host_verifications", result.SensitiveFields);
    }

    [Fact]
    public void Sensitive_field_without_approval_flag_is_allowed()
    {
        var result = Evaluator.Evaluate("""[{"$match":{"host.host_identity_verified":true}}]""");

        Assert.Equal(GuardrailOutcome.Allowed, result.Outcome);
    }

    [Fact]
    public void Parent_reference_to_sensitive_document_pauses()
    {
        var result = Evaluator.Evaluate("""[{"$match":{"address":"x"}}]""");

        Assert.Equal(GuardrailOutcome.PausedForApproval, result.Outcome);
        Assert.Contains("address.location.coordinates", result.SensitiveFields);
    }
}
