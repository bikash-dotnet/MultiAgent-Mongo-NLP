using Gateway.Nlp.Guardrails;
using Gateway.Reports;

namespace Gateway.Tests.Reports;

public class DemoReportPipelineTests
{
    [Fact]
    public void Produces_a_read_only_parsable_pipeline()
    {
        var pipeline = DemoReportPipeline.FromUtterance("average price by market");

        var analysis = MqlAnalyzer.Analyze(pipeline);

        Assert.True(analysis.IsParsable);
        Assert.Empty(ReadOnlyRule.FindViolations(analysis));
    }
}
