using System.Text.Json;
using Gateway.Configuration;
using Microsoft.Extensions.Options;

namespace Gateway.Nlp.Llm;

public sealed class SelfCorrectingLlmQueryGenerator
{
    private readonly ILlmQueryGenerator _generator;
    private readonly IPipelineValidator _validator;
    private readonly ISettingsProvider _settings;

    public SelfCorrectingLlmQueryGenerator(
        ILlmQueryGenerator generator,
        IPipelineValidator validator,
        ISettingsProvider settings)
    {
        _generator = generator;
        _validator = validator;
        _settings = settings;
    }

    public async Task<SelfCorrectionResult> GenerateAsync(
        string utterance,
        string slotsJson,
        CancellationToken cancellationToken)
    {
        var maxAttempts = GetMaxAttempts();
        var tokens = 0;
        string? previousError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
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

        return new SelfCorrectionResult(null, maxAttempts, tokens, previousError ?? "pipeline validation failed");
    }

    private int GetMaxAttempts()
    {
        if (_settings.TryGetValue("NvidiaNim", "MaxAttempts", out var value) &&
            value.ValueKind == JsonValueKind.Number)
        {
            return Math.Max(1, value.GetInt32());
        }
        return 3;
    }
}
