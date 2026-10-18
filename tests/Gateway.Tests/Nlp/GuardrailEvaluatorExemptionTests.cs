using Gateway.Nlp.Guardrails;

namespace Gateway.Tests.Nlp;

public class GuardrailEvaluatorExemptionTests
{
    private const string CoordinatesPipeline =
        """[{"$match":{"address.location.coordinates.lat":{"$gte":40}}}]""";

    [Fact]
    public async Task Data_owner_is_exempt_from_the_approval_pause()
    {
        var evaluator = GuardrailTestFactory.FromAssets();

        var result = await evaluator.EvaluateAsync(CoordinatesPipeline, "Data Owner / Admin");

        Assert.Equal(GuardrailOutcome.Allowed, result.Outcome);
        Assert.Equal(AccessRequest.ExemptionOwnerAccess, result.ExemptionType);
        Assert.Contains("address.location.coordinates", result.SensitiveFields);
    }

    [Fact]
    public async Task Business_analyst_is_not_exempt()
    {
        var evaluator = GuardrailTestFactory.FromAssets();

        var result = await evaluator.EvaluateAsync(CoordinatesPipeline, "Business Analyst");

        Assert.Equal(GuardrailOutcome.PausedForApproval, result.Outcome);
        Assert.Null(result.ExemptionType);
    }

    [Fact]
    public async Task Unknown_role_is_not_exempt()
    {
        var evaluator = GuardrailTestFactory.FromAssets();

        var result = await evaluator.EvaluateAsync(CoordinatesPipeline, null);

        Assert.Equal(GuardrailOutcome.PausedForApproval, result.Outcome);
    }
}
