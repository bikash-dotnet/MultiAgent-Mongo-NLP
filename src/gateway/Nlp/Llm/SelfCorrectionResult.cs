namespace Gateway.Nlp.Llm;

public sealed record SelfCorrectionResult(string? Pipeline, int Attempts, int TokensConsumed, string? Error);
