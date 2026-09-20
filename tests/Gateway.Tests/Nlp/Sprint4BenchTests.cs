using System.Diagnostics;
using Gateway.Nlp.Guardrails;

namespace Gateway.Tests.Nlp;

public class Sprint4BenchTests
{
    private const int Iterations = 2000;

    [Fact]
    public void Guardrail_step_stays_under_one_millisecond()
    {
        var evaluator = GuardrailTestFactory.FromAssets();
        const string pipeline =
            """[{"$match":{"price":{"$lte":200},"address.market":"New York"}},{"$sort":{"review_scores.rating":-1}},{"$limit":10}]""";

        for (var i = 0; i < 50; i++)
        {
            evaluator.Evaluate(pipeline);
        }

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < Iterations; i++)
        {
            evaluator.Evaluate(pipeline);
        }
        sw.Stop();

        var perCall = sw.Elapsed.TotalMilliseconds / Iterations;

        var content =
            "# Sprint 4 — BRD-NFR-01 Guardrail Benchmarks\n" +
            "\n" +
            "AST analysis, read-only enforcement, schema whitelist, and field-flag verification for a typical pipeline.\n" +
            "\n" +
            "| Step | Measured | Budget |\n" +
            "| --- | --- | --- |\n" +
            $"| guardrail (analyze + rules + registry) | {perCall:F4} ms | < 1 ms |\n";

        var path = Path.Combine(TestPaths.RepoRoot(), "docs", "benchmarks", "sprint-4-nfr01.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);

        Assert.True(perCall < 1.0, $"guardrail step {perCall:F4} ms exceeded 1 ms budget");
    }
}
