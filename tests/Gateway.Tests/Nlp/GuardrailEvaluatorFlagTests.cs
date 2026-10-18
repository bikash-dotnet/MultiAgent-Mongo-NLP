using Gateway.Governance;
using Gateway.Nlp.Guardrails;

namespace Gateway.Tests.Nlp;

public class GuardrailEvaluatorFlagTests
{
    private const string CoordinatesPipeline =
        """[{"$match":{"address.location.coordinates.lat":{"$gte":40}}}]""";

    [Fact]
    public async Task Pauses_sensitive_approval_fields_when_the_flag_is_on()
    {
        var evaluator = GuardrailTestFactory.FromAssets(new InMemoryApprovalFlagStore(true));

        var result = await evaluator.EvaluateAsync(CoordinatesPipeline);

        Assert.Equal(GuardrailOutcome.PausedForApproval, result.Outcome);
        Assert.Contains("address.location.coordinates", result.SensitiveFields);
    }

    [Fact]
    public async Task Allows_but_reports_sensitive_fields_when_the_flag_is_off()
    {
        var evaluator = GuardrailTestFactory.FromAssets(new InMemoryApprovalFlagStore(false));

        var result = await evaluator.EvaluateAsync(CoordinatesPipeline);

        Assert.Equal(GuardrailOutcome.Allowed, result.Outcome);
        Assert.Contains("address.location.coordinates", result.SensitiveFields);
    }

    [Fact]
    public async Task Still_rejects_write_operators_when_the_flag_is_off()
    {
        var evaluator = GuardrailTestFactory.FromAssets(new InMemoryApprovalFlagStore(false));

        var result = await evaluator.EvaluateAsync("""[{"$out":"archive"}]""");

        Assert.Equal(GuardrailOutcome.Rejected, result.Outcome);
    }
}
