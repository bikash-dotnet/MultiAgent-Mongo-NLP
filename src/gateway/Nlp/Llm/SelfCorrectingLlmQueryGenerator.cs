using Microsoft.Extensions.Options;

namespace Gateway.Nlp.Llm;

public sealed class SelfCorrectingLlmQueryGenerator
{
    private readonly ILlmQueryGenerator _generator;
    private readonly IPipelineValidator _validator;
    private readonly int _maxAttempts;

    public SelfCorrectingLlmQueryGenerator(
        ILlmQueryGenerator generator,
        IPipelineValidator validator,
        IOptions<NvidiaNimOptions> options)
    {
        _generator = generator;
        _validator = validator;
        _maxAttempts = Math.Max(1, options.Value.MaxAttempts);
    }

    public async Task<SelfCorrectionResult> GenerateAsync(
        string utterance,
        string slotsJson,
        CancellationToken cancellationToken)
    {
        var tokens = 0;
        string? previousError = null;

        for (var attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            var generated = await _generator.GenerateAsync(utterance, slotsJson, previousError, cancellationToken);
            tokens += generated.TokensConsumed;

            var validation = _validator.Validate(generated.Pipeline);
            if (validation.IsValid)
            {
                return new SelfCorrectionResult(generated.Pipeline, attempt, tokens, null);
            }

            previousError = validation.Error;
        }

        return new SelfCorrectionResult(null, _maxAttempts, tokens, previousError ?? "pipeline validation failed");
    }
}
