namespace Gateway.Nlp.Llm;

public sealed record PipelineValidationResult(bool IsValid, string? Error);
