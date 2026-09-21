using Gateway.Governance;
using Gateway.Nlp.Guardrails;

namespace Gateway.Tests.Nlp;

public class GuardrailEvaluatorFlagTests
{
    private const string CoordinatesPipeline =
        """[{"$match":{"address.location.coordinates.lat":{"$gte":40}}}]""";

    [Fact]
    public void Pauses_sensitive_approval_fields_when_the_flag_is_on()
    {
        var evaluator = GuardrailTestFactory.FromAssets(new InMemoryApprovalFlagStore(true));

        var result = evaluator.Evaluate(CoordinatesPipeline);

        Assert.Equal(GuardrailOutcome.PausedForApproval, result.Outcome);
        Assert.Contains("address.location.coordinates", result.SensitiveFields);
    }

    [Fact]
    public void Allows_but_reports_sensitive_fields_when_the_flag_is_off()
    {
        var evaluator = GuardrailTestFactory.FromAssets(new InMemoryApprovalFlagStore(false));

        var result = evaluator.Evaluate(CoordinatesPipeline);

        Assert.Equal(GuardrailOutcome.Allowed, result.Outcome);
        Assert.Contains("address.location.coordinates", result.SensitiveFields);
    }

    [Fact]
    public void Still_rejects_write_operators_when_the_flag_is_off()
    {
        var evaluator = GuardrailTestFactory.FromAssets(new InMemoryApprovalFlagStore(false));

        var result = evaluator.Evaluate("""[{"$out":"archive"}]""");

        Assert.Equal(GuardrailOutcome.Rejected, result.Outcome);
    }
}
